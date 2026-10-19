// Copyright (c) IBM Corporation. All rights reserved.

using System;
using Microsoft.Extensions.VectorData;

namespace IBM.VectorData.Db2.UnitTests;

public class Db2ModelBuilderTests
{
    [Fact]
    public void UnsupportedIndexKind_Throws()
    {
        VectorStoreCollectionDefinition definition = new()
        {
            Properties =
            [
                new VectorStoreKeyProperty("Id", typeof(int)),
                new VectorStoreVectorProperty("Embedding", typeof(ReadOnlyMemory<float>), 3) { IndexKind = IndexKind.Hnsw },
            ],
        };

        Assert.Throws<NotSupportedException>(() => new Db2ModelBuilder().BuildDynamic(definition, defaultEmbeddingGenerator: null));
    }

    [Fact]
    public void UnsupportedDistanceFunction_Throws()
    {
        VectorStoreCollectionDefinition definition = new()
        {
            Properties =
            [
                new VectorStoreKeyProperty("Id", typeof(int)),
                new VectorStoreVectorProperty("Embedding", typeof(ReadOnlyMemory<float>), 3) { DistanceFunction = "Chebyshev" },
            ],
        };

        Assert.Throws<NotSupportedException>(() => new Db2ModelBuilder().BuildDynamic(definition, defaultEmbeddingGenerator: null));
    }

    [Theory]
    [InlineData(typeof(byte))]
    [InlineData(typeof(DateTimeOffset))]
    public void UnsupportedDataType_Throws(Type type)
    {
        VectorStoreCollectionDefinition definition = new()
        {
            Properties =
            [
                new VectorStoreKeyProperty("Id", typeof(int)),
                new VectorStoreDataProperty("Value", type),
            ],
        };

        Assert.Throws<NotSupportedException>(() => new Db2ModelBuilder().BuildDynamic(definition, defaultEmbeddingGenerator: null));
    }

    [Fact]
    public void UnsupportedKeyType_Throws()
    {
        VectorStoreCollectionDefinition definition = new() { Properties = [new VectorStoreKeyProperty("Id", typeof(DateTime))] };

        Assert.Throws<NotSupportedException>(() => new Db2ModelBuilder().BuildDynamic(definition, defaultEmbeddingGenerator: null));
    }
}
