// Copyright (c) IBM Corporation. All rights reserved.

using System;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;

namespace IBM.VectorData.Db2.UnitTests;

public class Db2ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDb2VectorStore_UsesEmbeddingGeneratorFromServices()
    {
        ServiceCollection services = new();
        services.AddSingleton<IEmbeddingGenerator>(new FakeEmbeddingGenerator());
        services.AddDb2VectorStore(_ => TestSupport.UnreachableConnectionString, _ => new Db2VectorStoreOptions { Schema = "S" });

        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<VectorStore>();

        Assert.Same(provider.GetRequiredService<Db2VectorStore>(), store);
        // Only works if the generator registered in DI reached the store options.
        using var collection = store.GetCollection<Guid, GuidHotel>("hotels");
    }

    [Fact]
    public void AddKeyedDb2VectorStore_KeepsExplicitGenerator()
    {
        ServiceCollection services = new();
        services.AddKeyedDb2VectorStore("key", _ => TestSupport.UnreachableConnectionString, _ => new Db2VectorStoreOptions { EmbeddingGenerator = new FakeEmbeddingGenerator() });

        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredKeyedService<VectorStore>("key");

        using var collection = store.GetCollection<Guid, GuidHotel>("hotels");
        Assert.Null(provider.GetService<VectorStore>());
    }

    [Fact]
    public void AddDb2VectorStore_WithoutGenerator()
    {
        ServiceCollection services = new();
        services.AddDb2VectorStore(_ => TestSupport.UnreachableConnectionString);

        using var provider = services.BuildServiceProvider();

        Assert.IsType<Db2VectorStore>(provider.GetRequiredService<VectorStore>());
    }

    [Fact]
    public void AddDb2Collection_RegistersCollectionInterfaces()
    {
        ServiceCollection services = new();
        services.AddSingleton<IEmbeddingGenerator>(new FakeEmbeddingGenerator());
        services.AddDb2Collection<Guid, GuidHotel>("hotels", TestSupport.UnreachableConnectionString);

        using var provider = services.BuildServiceProvider();
        var collection = provider.GetRequiredService<VectorStoreCollection<Guid, GuidHotel>>();

        Assert.Equal("hotels", collection.Name);
        Assert.Same(collection, provider.GetRequiredService<Db2Collection<Guid, GuidHotel>>());
        Assert.Same(collection, provider.GetRequiredService<IVectorSearchable<GuidHotel>>());
    }

    [Fact]
    public void AddKeyedDb2Collection_WithOptions()
    {
        ServiceCollection services = new();
        services.AddKeyedDb2Collection<int, Hotel>(
            "key", "hotels", _ => TestSupport.UnreachableConnectionString, _ => new Db2CollectionOptions { Schema = "S", EmbeddingGenerator = new FakeEmbeddingGenerator() });

        using var provider = services.BuildServiceProvider();

        Assert.Equal("hotels", provider.GetRequiredKeyedService<VectorStoreCollection<int, Hotel>>("key").Name);
    }

    [Fact]
    public void AddDb2Collection_WithoutGenerator()
    {
        ServiceCollection services = new();
        services.AddDb2Collection<int, Hotel>("hotels", _ => TestSupport.UnreachableConnectionString);

        using var provider = services.BuildServiceProvider();

        Assert.Equal("hotels", provider.GetRequiredService<VectorStoreCollection<int, Hotel>>().Name);
    }

    [Fact]
    public void Arguments_AreValidated()
    {
        ServiceCollection services = new();

        Assert.Throws<ArgumentNullException>(() => Db2ServiceCollectionExtensions.AddDb2VectorStore(null!, _ => ""));
        Assert.Throws<ArgumentNullException>(() => services.AddDb2VectorStore(null!));
        Assert.Throws<ArgumentException>(() => services.AddDb2Collection<int, Hotel>("", _ => ""));
        Assert.Throws<ArgumentException>(() => services.AddDb2Collection<int, Hotel>("hotels", connectionString: ""));
    }
}
