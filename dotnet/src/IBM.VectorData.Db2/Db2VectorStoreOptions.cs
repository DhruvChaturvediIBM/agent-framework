// Copyright (c) IBM Corporation. All rights reserved.

using Microsoft.Extensions.AI;

namespace IBM.VectorData.Db2;

/// <summary>
/// Options for creating a <see cref="Db2VectorStore"/>.
/// </summary>
public sealed class Db2VectorStoreOptions
{
    internal static readonly Db2VectorStoreOptions Default = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Db2VectorStoreOptions"/> class.
    /// </summary>
    public Db2VectorStoreOptions()
    {
    }

    internal Db2VectorStoreOptions(Db2VectorStoreOptions? source)
    {
        this.Schema = source?.Schema;
        this.EmbeddingGenerator = source?.EmbeddingGenerator;
    }

    /// <summary>
    /// Gets or sets the database schema. When not set, the connection's <c>CURRENT SCHEMA</c> is used.
    /// </summary>
    public string? Schema { get; set; }

    /// <summary>
    /// Gets or sets the default embedding generator to use when generating vectors embeddings with this vector store.
    /// </summary>
    public IEmbeddingGenerator? EmbeddingGenerator { get; set; }
}
