// Copyright (c) IBM Corporation. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.VectorData;

namespace IBM.VectorData.Db2.UnitTests;

public class Db2VectorStoreTests
{
    [Fact]
    public void Constructor_ValidatesConnectionString()
    {
        Assert.Throws<ArgumentNullException>(() => new Db2VectorStore(null!));
        Assert.Throws<ArgumentException>(() => new Db2VectorStore(""));
    }

    [Fact]
    public void GetCollection_PassesStoreOptions()
    {
        FakeEmbeddingGenerator generator = new();
        using var store = new Db2VectorStore(TestSupport.UnreachableConnectionString, new() { Schema = "S", EmbeddingGenerator = generator });

        // A string vector property needs an embedding generator, which comes from the store options.
        using var collection = store.GetCollection<Guid, GuidHotel>("hotels");
        Assert.Equal("hotels", collection.Name);

        using var dynamicCollection = store.GetDynamicCollection("dynamic", new() { Properties = [new VectorStoreKeyProperty("Key", typeof(int))] });
        Assert.Equal("dynamic", dynamicCollection.Name);
    }

    [Fact]
    public void GetCollection_RejectsDictionaryRecords()
    {
        using var store = new Db2VectorStore(TestSupport.UnreachableConnectionString);

        Assert.Throws<ArgumentException>(() => store.GetCollection<object, Dictionary<string, object?>>("c"));
    }

    [Fact]
    public void GetService_ReturnsMetadataAndSelf()
    {
        using var store = new Db2VectorStore(TestSupport.UnreachableConnectionString);

        var metadata = Assert.IsType<VectorStoreMetadata>(store.GetService(typeof(VectorStoreMetadata)));
        Assert.Equal("ibm.db2", metadata.VectorStoreSystemName);
        Assert.Equal("TESTDB", metadata.VectorStoreName);

        Assert.Same(store, store.GetService(typeof(Db2VectorStore)));
        Assert.Null(store.GetService(typeof(VectorStoreMetadata), serviceKey: "key"));
        Assert.Null(store.GetService(typeof(string)));
        Assert.Throws<ArgumentNullException>(() => store.GetService(null!));
    }

    [Fact]
    public async Task DatabaseErrors_AreWrappedAsync()
    {
        using var store = new Db2VectorStore(TestSupport.UnreachableConnectionString);

        var ex = await Assert.ThrowsAsync<VectorStoreException>(() => store.ListCollectionNamesAsync().ToListAsync().AsTask());
        Assert.Equal("ListCollectionNames", ex.OperationName);
        Assert.Null(ex.CollectionName);

        ex = await Assert.ThrowsAsync<VectorStoreException>(() => store.CollectionExistsAsync("c"));
        Assert.Equal("c", ex.CollectionName);

        ex = await Assert.ThrowsAsync<VectorStoreException>(() => store.EnsureCollectionDeletedAsync("c"));
        Assert.Equal("DeleteCollection", ex.OperationName);
    }

    [Theory]
    [InlineData("Server=h:1;Database=MYDB;UID=u;PWD=p;", "MYDB")]
    [InlineData("Server=h:1;DB=OTHER;UID=u;PWD=p;", "OTHER")]
    [InlineData("Server=h:1;UID=u;PWD=p;", null)]
    public void DatabaseName_IsReadFromConnectionString(string connectionString, string? expected)
    {
        Assert.Equal(expected, Db2ConnectionString.GetDatabase(connectionString));
    }
}
