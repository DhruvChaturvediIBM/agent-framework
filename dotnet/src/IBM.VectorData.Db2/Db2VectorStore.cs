// Copyright (c) IBM Corporation. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
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
/// An implementation of <see cref="VectorStore"/> backed by an IBM Db2 database (12.1.2 or later).
/// </summary>
public sealed class Db2VectorStore : VectorStore
{
    /// <summary>A general purpose definition that can be used to construct a collection when needing to proxy schema agnostic operations.</summary>
    private static readonly VectorStoreCollectionDefinition s_generalPurposeDefinition = new() { Properties = [new VectorStoreKeyProperty("Key", typeof(string))] };

    private readonly string _connectionString;
    private readonly string? _schema;
    private readonly IEmbeddingGenerator? _embeddingGenerator;
    private readonly VectorStoreMetadata _metadata;

    /// <summary>
    /// Initializes a new instance of the <see cref="Db2VectorStore"/> class.
    /// </summary>
    /// <param name="connectionString">The Db2 connection string.</param>
    /// <param name="options">Optional configuration options.</param>
    [RequiresUnreferencedCode("The Db2 provider is currently incompatible with trimming.")]
    [RequiresDynamicCode("The Db2 provider is currently incompatible with NativeAOT.")]
    public Db2VectorStore(string connectionString, Db2VectorStoreOptions? options = null)
    {
        Throw.IfNullOrWhitespace(connectionString);

        options ??= Db2VectorStoreOptions.Default;

        this._connectionString = connectionString;
        this._schema = options.Schema;
        this._embeddingGenerator = options.EmbeddingGenerator;

        this._metadata = new()
        {
            VectorStoreSystemName = Db2Constants.VectorStoreSystemName,
            VectorStoreName = Db2ConnectionString.GetDatabase(connectionString),
        };
    }

    /// <inheritdoc/>
    [RequiresUnreferencedCode("The Db2 provider is currently incompatible with trimming.")]
    [RequiresDynamicCode("The Db2 provider is currently incompatible with NativeAOT.")]
    public override Db2Collection<TKey, TRecord> GetCollection<TKey, TRecord>(string name, VectorStoreCollectionDefinition? definition = null)
        => typeof(TRecord) == typeof(Dictionary<string, object?>)
            ? throw new ArgumentException(VectorDataStrings.GetCollectionWithDictionaryNotSupported)
            : new Db2Collection<TKey, TRecord>(
                this._connectionString,
                name,
                new()
                {
                    Schema = this._schema,
                    Definition = definition,
                    EmbeddingGenerator = this._embeddingGenerator,
                });

    /// <inheritdoc/>
#pragma warning disable IL2026, IL3050 // The base method is not annotated; a dynamic collection maps no CLR record type.
    public override Db2DynamicCollection GetDynamicCollection(string name, VectorStoreCollectionDefinition definition)
        => new(
            this._connectionString,
            name,
            new()
            {
                Schema = this._schema,
                Definition = definition,
                EmbeddingGenerator = this._embeddingGenerator,
            });
#pragma warning restore IL2026, IL3050

    /// <inheritdoc/>
    public override async IAsyncEnumerable<string> ListCollectionNamesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        VectorStoreCollectionMetadata metadata = new()
        {
            VectorStoreSystemName = this._metadata.VectorStoreSystemName,
            VectorStoreName = this._metadata.VectorStoreName,
        };

        using DB2Connection connection = new(this._connectionString);
        await Db2ErrorHandler.RunAsync(metadata, "ListCollectionNames", () => connection.OpenAsync(cancellationToken)).ConfigureAwait(false);

        using DB2Command command = Db2CommandBuilder.SelectTableNames(connection, this._schema);
        using DbDataReader reader = await Db2ErrorHandler.RunAsync(metadata, "ListCollectionNames",
            () => command.ExecuteReaderAsync(cancellationToken)).ConfigureAwait(false);

        while (await Db2ErrorHandler.RunAsync(metadata, "ListCollectionNames", () => reader.ReadAsync(cancellationToken)).ConfigureAwait(false))
        {
            yield return reader.GetString(0);
        }
    }

    /// <inheritdoc/>
    public override Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
        => this.GetGeneralPurposeCollection(name).CollectionExistsAsync(cancellationToken);

    /// <inheritdoc/>
    public override Task EnsureCollectionDeletedAsync(string name, CancellationToken cancellationToken = default)
        => this.GetGeneralPurposeCollection(name).EnsureCollectionDeletedAsync(cancellationToken);

    /// <inheritdoc/>
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        Throw.IfNull(serviceType);

        return
            serviceKey is not null ? null :
            serviceType == typeof(VectorStoreMetadata) ? this._metadata :
            serviceType.IsInstanceOfType(this) ? this :
            null;
    }

    private Db2DynamicCollection GetGeneralPurposeCollection(string name)
        => this.GetDynamicCollection(name, s_generalPurposeDefinition);
}
