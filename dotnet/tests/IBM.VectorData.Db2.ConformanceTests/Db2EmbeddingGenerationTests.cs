// Copyright (c) IBM Corporation. All rights reserved.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;

namespace IBM.VectorData.Db2.ConformanceTests;

public class Db2EmbeddingGenerationTests(Db2EmbeddingGenerationTests.StringVectorFixture stringVectorFixture, Db2EmbeddingGenerationTests.RomOfFloatVectorFixture romOfFloatVectorFixture)
    : EmbeddingGenerationTests<int>(stringVectorFixture, romOfFloatVectorFixture), IClassFixture<Db2EmbeddingGenerationTests.StringVectorFixture>, IClassFixture<Db2EmbeddingGenerationTests.RomOfFloatVectorFixture>
{
    public new class StringVectorFixture : EmbeddingGenerationTests<int>.StringVectorFixture
    {
        public override TestStore TestStore => Db2TestStore.Instance;

        public override VectorStore CreateVectorStore(IEmbeddingGenerator? embeddingGenerator)
            => Db2TestStore.Instance.GetVectorStore(new() { EmbeddingGenerator = embeddingGenerator });

        public override Func<IServiceCollection, IServiceCollection>[] DependencyInjectionStoreRegistrationDelegates =>
        [
            services => services.AddDb2VectorStore(sp => Db2TestStore.Instance.ConnectionString)
        ];

        public override Func<IServiceCollection, IServiceCollection>[] DependencyInjectionCollectionRegistrationDelegates =>
        [
            services => services.AddDb2Collection<int, RecordWithAttributes>(this.CollectionName, sp => Db2TestStore.Instance.ConnectionString),
            services => services.AddDb2Collection<int, RecordWithAttributes>(this.CollectionName, Db2TestStore.Instance.ConnectionString),
        ];
    }

    public new class RomOfFloatVectorFixture : EmbeddingGenerationTests<int>.RomOfFloatVectorFixture
    {
        public override TestStore TestStore => Db2TestStore.Instance;

        public override VectorStore CreateVectorStore(IEmbeddingGenerator? embeddingGenerator)
            => Db2TestStore.Instance.GetVectorStore(new() { EmbeddingGenerator = embeddingGenerator });

        public override Func<IServiceCollection, IServiceCollection>[] DependencyInjectionStoreRegistrationDelegates =>
        [
            services => services.AddDb2VectorStore(sp => Db2TestStore.Instance.ConnectionString)
        ];

        public override Func<IServiceCollection, IServiceCollection>[] DependencyInjectionCollectionRegistrationDelegates =>
        [
            services => services.AddDb2Collection<int, RecordWithAttributes>(this.CollectionName, sp => Db2TestStore.Instance.ConnectionString),
            services => services.AddDb2Collection<int, RecordWithAttributes>(this.CollectionName, Db2TestStore.Instance.ConnectionString),
        ];
    }
}
