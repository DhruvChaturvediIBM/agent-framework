// Copyright (c) IBM Corporation. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace IBM.VectorData.Db2;

/// <summary>
/// Represents a collection of vector store records in a Db2 table, mapped to a dynamic <c>Dictionary&lt;string, object?&gt;</c>.
/// </summary>
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public sealed class Db2DynamicCollection : Db2Collection<object, Dictionary<string, object?>>
#pragma warning restore CA1711
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Db2DynamicCollection"/> class.
    /// </summary>
    /// <param name="connectionString">The Db2 connection string.</param>
    /// <param name="name">The name of the collection, used as the table name.</param>
    /// <param name="options">Configuration options; <see cref="Microsoft.Extensions.VectorData.VectorStoreCollectionOptions.Definition"/> is required.</param>
    [RequiresUnreferencedCode("The Db2 provider is currently incompatible with trimming.")]
    [RequiresDynamicCode("The Db2 provider is currently incompatible with NativeAOT.")]
    public Db2DynamicCollection(string connectionString, string name, Db2CollectionOptions options)
        : base(
            connectionString,
            name,
            static options => new Db2ModelBuilder()
                .BuildDynamic(
                    options.Definition ?? throw new ArgumentException("A definition is required for dynamic collections."),
                    options.EmbeddingGenerator),
            options)
    {
    }
}
