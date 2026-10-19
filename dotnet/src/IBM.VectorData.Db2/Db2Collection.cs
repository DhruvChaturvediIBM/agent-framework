// Copyright (c) IBM Corporation. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using IBM.Data.Db2;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;
using Microsoft.Shared.Diagnostics;

namespace IBM.VectorData.Db2;

/// <summary>
/// An implementation of <see cref="VectorStoreCollection{TKey, TRecord}"/> backed by an IBM Db2 table.
/// </summary>
/// <typeparam name="TKey">The data type of the record key. Can be <see cref="int"/>, <see cref="long"/>, <see cref="string"/> or <see cref="Guid"/>.</typeparam>
/// <typeparam name="TRecord">The data model to use for adding, updating and retrieving data from storage.</typeparam>
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix (Collection)
public class Db2Collection<TKey, TRecord> : VectorStoreCollection<TKey, TRecord>
#pragma warning restore CA1711
    where TKey : notnull
    where TRecord : class
{
    private static readonly VectorSearchOptions<TRecord> s_defaultVectorSearchOptions = new();

    private readonly VectorStoreCollectionMetadata _collectionMetadata;
    private readonly string _connectionString;
    private readonly string? _schema;
    private readonly CollectionModel _model;
    private readonly Db2Mapper<TRecord> _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="Db2Collection{TKey, TRecord}"/> class.
    /// </summary>
    /// <param name="connectionString">The Db2 connection string.</param>
    /// <param name="name">The name of the collection, used as the table name.</param>
    /// <param name="options">Optional configuration options.</param>
    [RequiresUnreferencedCode("The Db2 provider is currently incompatible with trimming.")]
    [RequiresDynamicCode("The Db2 provider is currently incompatible with NativeAOT.")]
    public Db2Collection(string connectionString, string name, Db2CollectionOptions? options = null)
        : this(
            connectionString,
            name,
            static options => typeof(TRecord) == typeof(Dictionary<string, object?>)
                ? throw new NotSupportedException(VectorDataStrings.NonDynamicCollectionWithDictionaryNotSupported(typeof(Db2DynamicCollection)))
                : new Db2ModelBuilder().Build(typeof(TRecord), typeof(TKey), options.Definition, options.EmbeddingGenerator),
            options)
    {
    }

    internal Db2Collection(string connectionString, string name, Func<Db2CollectionOptions, CollectionModel> modelFactory, Db2CollectionOptions? options)
    {
        Throw.IfNullOrWhitespace(connectionString);
        Throw.IfNullOrWhitespace(name);

        options ??= Db2CollectionOptions.Default;

        this._connectionString = connectionString;
        this._schema = options.Schema;
        this.Name = name;
        this._model = modelFactory(options);
        this._mapper = new Db2Mapper<TRecord>(this._model);

        this._collectionMetadata = new()
        {
            VectorStoreSystemName = Db2Constants.VectorStoreSystemName,
            VectorStoreName = Db2ConnectionString.GetDatabase(connectionString),
            CollectionName = name,
        };
    }

    /// <inheritdoc/>
    public override string Name { get; }

    /// <inheritdoc/>
    public override async Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        using DB2Connection connection = await this.OpenConnectionAsync("CollectionExists", cancellationToken).ConfigureAwait(false);
        using DB2Command command = Db2CommandBuilder.SelectTableName(connection, this._schema, this.Name);

        return await Db2ErrorHandler.RunAsync(this._collectionMetadata, "CollectionExists", async () =>
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        if (await this.CollectionExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        using DB2Connection connection = await this.OpenConnectionAsync("CreateCollection", cancellationToken).ConfigureAwait(false);

        // Db2 DDL is transactional, so the table and its indexes are created together or not at all.
        await this.RunInTransactionAsync(connection, "CreateCollection", async transaction =>
        {
            foreach (DB2Command command in Db2CommandBuilder.CreateTable(connection, this._schema, this.Name, this._model))
            {
                using (command)
                {
                    command.Transaction = transaction;
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
    {
        using DB2Connection connection = await this.OpenConnectionAsync("DeleteCollection", cancellationToken).ConfigureAwait(false);
        using DB2Command command = Db2CommandBuilder.DropTableIfExists(connection, this._schema, this.Name);

        await Db2ErrorHandler.RunAsync(this._collectionMetadata, "DeleteCollection",
            () => command.ExecuteNonQueryAsync(cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task<TRecord?> GetAsync(TKey key, RecordRetrievalOptions? options = null, CancellationToken cancellationToken = default)
    {
        Throw.IfNull(key);

        bool includeVectors = options?.IncludeVectors is true;
        if (includeVectors && this._model.EmbeddingGenerationRequired)
        {
            throw new NotSupportedException(VectorDataStrings.IncludeVectorsNotSupportedWithEmbeddingGeneration);
        }

        using DB2Connection connection = await this.OpenConnectionAsync("Get", cancellationToken).ConfigureAwait(false);
        using DB2Command command = Db2CommandBuilder.SelectSingle(connection, this._schema, this.Name, this._model, key, includeVectors);

        return await Db2ErrorHandler.RunAsync(this._collectionMetadata, "Get", async () =>
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                ? this._mapper.MapFromStorageToDataModel(reader, includeVectors)
                : null;
        }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<TRecord> GetAsync(
        IEnumerable<TKey> keys,
        RecordRetrievalOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Throw.IfNull(keys);

        bool includeVectors = options?.IncludeVectors is true;
        if (includeVectors && this._model.EmbeddingGenerationRequired)
        {
            throw new NotSupportedException(VectorDataStrings.IncludeVectorsNotSupportedWithEmbeddingGeneration);
        }

        using DB2Connection connection = await this.OpenConnectionAsync("GetBatch", cancellationToken).ConfigureAwait(false);

        foreach (TKey[] chunk in keys.Chunk(Db2Constants.MaxParameterCount))
        {
            using DB2Command command = connection.CreateCommand();
            if (!Db2CommandBuilder.SelectMany(command, this._schema, this.Name, this._model, chunk, includeVectors))
            {
                yield break;
            }

            await foreach (var record in this.ReadRecordsAsync(command, includeVectors, "GetBatch", cancellationToken).ConfigureAwait(false))
            {
                yield return record;
            }
        }
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<TRecord> GetAsync(
        Expression<Func<TRecord, bool>> filter,
        int top,
        FilteredRecordRetrievalOptions<TRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Throw.IfNull(filter);
        Throw.IfLessThan(top, 1);

        options ??= new();
        if (options.IncludeVectors && this._model.EmbeddingGenerationRequired)
        {
            throw new NotSupportedException(VectorDataStrings.IncludeVectorsNotSupportedWithEmbeddingGeneration);
        }

        using DB2Connection connection = await this.OpenConnectionAsync("GetFiltered", cancellationToken).ConfigureAwait(false);
        using DB2Command command = Db2CommandBuilder.SelectWhere(filter, top, options, connection, this._schema, this.Name, this._model);

        await foreach (var record in this.ReadRecordsAsync(command, options.IncludeVectors, "GetFiltered", cancellationToken).ConfigureAwait(false))
        {
            yield return record;
        }
    }

    /// <inheritdoc/>
    public override async Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        Throw.IfNull(key);

        using DB2Connection connection = await this.OpenConnectionAsync("Delete", cancellationToken).ConfigureAwait(false);
        using DB2Command command = Db2CommandBuilder.DeleteSingle(connection, this._schema, this.Name, this._model.KeyProperty, key);

        await Db2ErrorHandler.RunAsync(this._collectionMetadata, "Delete",
            () => command.ExecuteNonQueryAsync(cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task DeleteAsync(IEnumerable<TKey> keys, CancellationToken cancellationToken = default)
    {
        Throw.IfNull(keys);

        using DB2Connection connection = await this.OpenConnectionAsync("DeleteBatch", cancellationToken).ConfigureAwait(false);

        await this.RunInTransactionAsync(connection, "DeleteBatch", async transaction =>
        {
            foreach (TKey[] chunk in keys.Chunk(Db2Constants.MaxParameterCount))
            {
                using DB2Command command = connection.CreateCommand();
                command.Transaction = transaction;

                if (Db2CommandBuilder.DeleteMany(command, this._schema, this.Name, this._model.KeyProperty, chunk))
                {
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override Task UpsertAsync(TRecord record, CancellationToken cancellationToken = default)
    {
        Throw.IfNull(record);

        return this.UpsertAsync([record], cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task UpsertAsync(IEnumerable<TRecord> records, CancellationToken cancellationToken = default)
    {
        Throw.IfNull(records);

        IReadOnlyList<TRecord> recordsList = records as IReadOnlyList<TRecord> ?? records.ToList();
        if (recordsList.Count == 0)
        {
            return;
        }

        // Generate embeddings once per vector property for all records.
        Dictionary<VectorPropertyModel, IReadOnlyList<Embedding>>? generatedEmbeddings = null;
        foreach (var vectorProperty in this._model.VectorProperties)
        {
            if (Db2ModelBuilder.IsVectorPropertyTypeValidCore(vectorProperty.Type, out _))
            {
                continue;
            }

            Debug.Assert(vectorProperty.EmbeddingGenerator is not null);
            generatedEmbeddings ??= new(this._model.VectorProperties.Count);
            generatedEmbeddings[vectorProperty] = await vectorProperty.GenerateEmbeddingsAsync(
                recordsList.Select(r => vectorProperty.GetValueAsObject(r)), cancellationToken).ConfigureAwait(false);
        }

        // Db2 has no server-side GUID default, so auto-generated GUID keys are assigned here.
        var keyProperty = this._model.KeyProperty;
        if (keyProperty.IsAutoGenerated)
        {
            foreach (var record in recordsList)
            {
                if (keyProperty.GetValueAsObject(record) is Guid guid && guid == Guid.Empty)
                {
                    keyProperty.SetValueAsObject(record, Guid.NewGuid());
                }
            }
        }

        using DB2Connection connection = await this.OpenConnectionAsync("Upsert", cancellationToken).ConfigureAwait(false);

        await this.RunInTransactionAsync(connection, "Upsert", async transaction =>
        {
            for (int i = 0; i < recordsList.Count; i++)
            {
                using DB2Command command = connection.CreateCommand();
                command.Transaction = transaction;

                int recordIndex = i;
                Db2CommandBuilder.Upsert(
                    command,
                    this._schema,
                    this.Name,
                    this._model,
                    recordsList[i],
                    generatedEmbeddings is null ? null : p => generatedEmbeddings.TryGetValue(p, out var e) ? e[recordIndex] : null);

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync<TInput>(
        TInput searchValue,
        int top,
        VectorSearchOptions<TRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Throw.IfNull(searchValue);
        Throw.IfLessThan(top, 1);

        options ??= s_defaultVectorSearchOptions;
        if (options.IncludeVectors && this._model.EmbeddingGenerationRequired)
        {
            throw new NotSupportedException(VectorDataStrings.IncludeVectorsNotSupportedWithEmbeddingGeneration);
        }

        var vectorProperty = this._model.GetVectorPropertyOrSingle(options);

        ReadOnlyMemory<float> vector = searchValue switch
        {
            ReadOnlyMemory<float> r => r,
            float[] f => f,
            Embedding<float> e => e.Vector,

            _ when vectorProperty.EmbeddingGenerationDispatcher is not null
                => ((Embedding<float>)await vectorProperty.GenerateEmbeddingAsync(searchValue, cancellationToken).ConfigureAwait(false)).Vector,

            _ => vectorProperty.EmbeddingGenerator is null
                ? throw new NotSupportedException(VectorDataStrings.InvalidSearchInputAndNoEmbeddingGeneratorWasConfigured(searchValue.GetType(), Db2ModelBuilder.SupportedVectorTypes))
                : throw new InvalidOperationException(VectorDataStrings.IncompatibleEmbeddingGeneratorWasConfiguredForInputType(typeof(TInput), vectorProperty.EmbeddingGenerator.GetType())),
        };

        using DB2Connection connection = await this.OpenConnectionAsync("Search", cancellationToken).ConfigureAwait(false);
        using DB2Command command = Db2CommandBuilder.SelectVector(connection, this._schema, this.Name, vectorProperty, this._model, top, options, vector);

        using DbDataReader reader = await Db2ErrorHandler.RunAsync(this._collectionMetadata, "Search",
            () => command.ExecuteReaderAsync(cancellationToken)).ConfigureAwait(false);

        int scoreOrdinal = -1;
        while (await Db2ErrorHandler.RunAsync(this._collectionMetadata, "Search",
            () => reader.ReadAsync(cancellationToken)).ConfigureAwait(false))
        {
            if (scoreOrdinal < 0)
            {
                scoreOrdinal = reader.GetOrdinal(Db2CommandBuilder.ScoreColumn);
            }

            yield return new VectorSearchResult<TRecord>(
                this._mapper.MapFromStorageToDataModel(reader, options.IncludeVectors),
                reader.GetDouble(scoreOrdinal));
        }
    }

    /// <inheritdoc />
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        Throw.IfNull(serviceType);

        return
            serviceKey is not null ? null :
            serviceType == typeof(VectorStoreCollectionMetadata) ? this._collectionMetadata :
            serviceType.IsInstanceOfType(this) ? this :
            null;
    }

    private async Task<DB2Connection> OpenConnectionAsync(string operationName, CancellationToken cancellationToken)
    {
        DB2Connection connection = new(this._connectionString);
        try
        {
            await Db2ErrorHandler.RunAsync(this._collectionMetadata, operationName,
                () => connection.OpenAsync(cancellationToken)).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private async Task RunInTransactionAsync(DB2Connection connection, string operationName, Func<DB2Transaction, Task> operation, CancellationToken cancellationToken)
    {
        using DB2Transaction transaction = connection.BeginTransaction();
        try
        {
            await operation(transaction).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw Db2ErrorHandler.Wrap(ex, this._collectionMetadata, operationName);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private async IAsyncEnumerable<TRecord> ReadRecordsAsync(
        DB2Command command,
        bool includeVectors,
        string operationName,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using DbDataReader reader = await Db2ErrorHandler.RunAsync(this._collectionMetadata, operationName,
            () => command.ExecuteReaderAsync(cancellationToken)).ConfigureAwait(false);

        while (await Db2ErrorHandler.RunAsync(this._collectionMetadata, operationName,
            () => reader.ReadAsync(cancellationToken)).ConfigureAwait(false))
        {
            yield return this._mapper.MapFromStorageToDataModel(reader, includeVectors);
        }
    }
}
