// Copyright (c) IBM Corporation. All rights reserved.

using Microsoft.Extensions.VectorData;

namespace IBM.VectorData.Db2;

/// <summary>
/// Options when creating a <see cref="Db2Collection{TKey, TRecord}"/>.
/// </summary>
public sealed class Db2CollectionOptions : VectorStoreCollectionOptions
{
    internal static readonly Db2CollectionOptions Default = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Db2CollectionOptions"/> class.
    /// </summary>
    public Db2CollectionOptions()
    {
    }

    internal Db2CollectionOptions(Db2CollectionOptions? source) : base(source)
    {
        this.Schema = source?.Schema;
    }

    /// <summary>
    /// Gets or sets the database schema. When not set, the connection's <c>CURRENT SCHEMA</c> is used.
    /// </summary>
    public string? Schema { get; set; }
}
