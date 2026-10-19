// Copyright (c) IBM Corporation. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.VectorData;

namespace IBM.VectorData.Db2.UnitTests;

public class Db2CollectionTests
{
    [Fact]
    public void Constructor_ValidatesArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new Db2Collection<int, Hotel>(null!, "hotels"));
        Assert.Throws<ArgumentException>(() => new Db2Collection<int, Hotel>(" ", "hotels"));
        Assert.Throws<ArgumentException>(() => new Db2Collection<int, Hotel>(TestSupport.UnreachableConnectionString, ""));
        Assert.Throws<NotSupportedException>(() => new Db2Collection<object, Dictionary<string, object?>>(TestSupport.UnreachableConnectionString, "hotels"));
    }

    [Fact]
    public void DynamicCollection_RequiresDefinition()
    {
        Assert.Throws<ArgumentException>(() => new Db2DynamicCollection(TestSupport.UnreachableConnectionString, "hotels", new Db2CollectionOptions()));
    }

    [Fact]
    public void GetService_ReturnsMetadataAndSelf()
    {
        using var collection = new Db2Collection<int, Hotel>(TestSupport.UnreachableConnectionString, "hotels", new() { Schema = "S" });

        var metadata = Assert.IsType<VectorStoreCollectionMetadata>(collection.GetService(typeof(VectorStoreCollectionMetadata)));
        Assert.Equal("ibm.db2", metadata.VectorStoreSystemName);
        Assert.Equal("TESTDB", metadata.VectorStoreName);
        Assert.Equal("hotels", metadata.CollectionName);
        Assert.Equal("hotels", collection.Name);

        Assert.Same(collection, collection.GetService(typeof(Db2Collection<int, Hotel>)));
        Assert.Null(collection.GetService(typeof(VectorStoreCollectionMetadata), serviceKey: "key"));
        Assert.Null(collection.GetService(typeof(string)));
        Assert.Throws<ArgumentNullException>(() => collection.GetService(null!));
    }

    [Fact]
    public async Task Operations_ValidateArgumentsAsync()
    {
        using var collection = new Db2Collection<string, GuidKeyed>(TestSupport.UnreachableConnectionString, "c");

        await Assert.ThrowsAsync<ArgumentNullException>(() => collection.GetAsync((string)null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => collection.GetAsync((IEnumerable<string>)null!).ToListAsync().AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => collection.GetAsync(null!, top: 1).ToListAsync().AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => collection.GetAsync(r => true, top: 0).ToListAsync().AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => collection.DeleteAsync((string)null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => collection.DeleteAsync((IEnumerable<string>)null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => collection.UpsertAsync((GuidKeyed)null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => collection.UpsertAsync((IEnumerable<GuidKeyed>)null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => collection.SearchAsync<float[]>(null!, top: 1).ToListAsync().AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => collection.SearchAsync(new float[] { 1, 2, 3 }, top: 0).ToListAsync().AsTask());
    }

    [Fact]
    public async Task IncludeVectors_IsRejectedWithEmbeddingGenerationAsync()
    {
        using var collection = new Db2Collection<Guid, GuidHotel>(
            TestSupport.UnreachableConnectionString, "c", new() { EmbeddingGenerator = new FakeEmbeddingGenerator() });

        await Assert.ThrowsAsync<NotSupportedException>(() => collection.GetAsync(Guid.NewGuid(), new() { IncludeVectors = true }));
        await Assert.ThrowsAsync<NotSupportedException>(() => collection.GetAsync([Guid.NewGuid()], new() { IncludeVectors = true }).ToListAsync().AsTask());
        await Assert.ThrowsAsync<NotSupportedException>(() => collection.GetAsync(r => true, 1, new() { IncludeVectors = true }).ToListAsync().AsTask());
        await Assert.ThrowsAsync<NotSupportedException>(() => collection.SearchAsync("query", 1, new() { IncludeVectors = true }).ToListAsync().AsTask());
    }

    [Fact]
    public async Task Search_WithTextAndNoGenerator_ThrowsAsync()
    {
        using var collection = new Db2Collection<int, Hotel>(TestSupport.UnreachableConnectionString, "c");

        await Assert.ThrowsAsync<NotSupportedException>(() => collection.SearchAsync("query", 1).ToListAsync().AsTask());
    }

    [Fact]
    public async Task Upsert_EmptyBatch_DoesNotConnectAsync()
    {
        using var collection = new Db2Collection<int, Hotel>(TestSupport.UnreachableConnectionString, "c");

        await collection.UpsertAsync([]);
    }

    [Fact]
    public async Task Upsert_GeneratesEmbeddingsAndGuidKeysBeforeWritingAsync()
    {
        FakeEmbeddingGenerator generator = new();
        using var collection = new Db2Collection<Guid, GuidHotel>(
            TestSupport.UnreachableConnectionString, "c", new() { EmbeddingGenerator = generator });
        GuidHotel[] records = [new() { Embedding = "first" }, new() { Embedding = "second" }];

        var ex = await Assert.ThrowsAsync<VectorStoreException>(() => collection.UpsertAsync(records));

        Assert.Equal("Upsert", ex.OperationName);
        Assert.Equal(["first", "second"], generator.Inputs);
        Assert.All(records, r => Assert.NotEqual(Guid.Empty, r.Id));
    }

    [Fact]
    public async Task DatabaseErrors_AreWrappedWithOperationNameAsync()
    {
        using var collection = new Db2Collection<int, Hotel>(TestSupport.UnreachableConnectionString, "hotels");

        async Task AssertWrappedAsync(string operation, Func<Task> action)
        {
            var ex = await Assert.ThrowsAsync<VectorStoreException>(action);
            Assert.Equal(operation, ex.OperationName);
            Assert.Equal("ibm.db2", ex.VectorStoreSystemName);
            Assert.Equal("TESTDB", ex.VectorStoreName);
            Assert.Equal("hotels", ex.CollectionName);
            Assert.IsType<Data.Db2.DB2Exception>(ex.InnerException);
        }

        await AssertWrappedAsync("CollectionExists", () => collection.CollectionExistsAsync());
        await AssertWrappedAsync("CollectionExists", () => collection.EnsureCollectionExistsAsync());
        await AssertWrappedAsync("DeleteCollection", () => collection.EnsureCollectionDeletedAsync());
        await AssertWrappedAsync("Get", () => collection.GetAsync(1));
        await AssertWrappedAsync("GetBatch", () => collection.GetAsync([1, 2]).ToListAsync().AsTask());
        await AssertWrappedAsync("GetFiltered", () => collection.GetAsync(r => r.Id > 1, 5).ToListAsync().AsTask());
        await AssertWrappedAsync("Delete", () => collection.DeleteAsync(1));
        await AssertWrappedAsync("DeleteBatch", () => collection.DeleteAsync([1, 2]));
        await AssertWrappedAsync("Upsert", () => collection.UpsertAsync(new Hotel { Id = 1 }));
        await AssertWrappedAsync("Search", () => collection.SearchAsync(new float[] { 1, 2, 3 }, 1).ToListAsync().AsTask());
        await AssertWrappedAsync("Search", () => collection.SearchAsync(new ReadOnlyMemory<float>([1, 2, 3]), 1).ToListAsync().AsTask());
        await AssertWrappedAsync("Search", () => collection.SearchAsync(new Microsoft.Extensions.AI.Embedding<float>(new float[] { 1, 2, 3 }), 1).ToListAsync().AsTask());
    }

    [Fact]
    public async Task Search_GeneratesQueryEmbeddingAsync()
    {
        FakeEmbeddingGenerator generator = new();
        using var collection = new Db2Collection<Guid, GuidHotel>(
            TestSupport.UnreachableConnectionString, "c", new() { EmbeddingGenerator = generator });

        await Assert.ThrowsAsync<VectorStoreException>(() => collection.SearchAsync("pirates", 1).ToListAsync().AsTask());

        Assert.Equal(["pirates"], generator.Inputs);
    }

    public sealed class GuidKeyed
    {
        [VectorStoreKey]
        public string Id { get; set; } = "";

        [VectorStoreVector(3)]
        public float[]? Embedding { get; set; }
    }
}
