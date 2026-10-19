// Copyright (c) IBM Corporation. All rights reserved.

using VectorData.ConformanceTests;
using VectorData.ConformanceTests.ModelTests;
using VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests.TypeTests;
using Xunit.Sdk;

// Binds each test suite of Microsoft.Extensions.VectorData.ConformanceTests to Db2.

namespace IBM.VectorData.Db2.ConformanceTests;

#pragma warning disable CS0252 // Possible unintended reference comparison; left hand side needs cast

public class Db2TestSuiteImplementationTests : TestSuiteImplementationTests
{
    // Hybrid search needs Db2 Text Search, which is not implemented yet.
    protected override ICollection<Type> IgnoredTestBases { get; } = [typeof(HybridSearchTests<>)];
}

public class Db2CollectionManagementTests(Db2CollectionManagementTests.Fixture fixture)
    : CollectionManagementTests<string>(fixture), IClassFixture<Db2CollectionManagementTests.Fixture>
{
    public class Fixture : VectorStoreFixture
    {
        public override TestStore TestStore => Db2TestStore.Instance;
    }
}

public class Db2DistanceFunctionTests(Db2DistanceFunctionTests.Fixture fixture)
    : DistanceFunctionTests<int>(fixture), IClassFixture<Db2DistanceFunctionTests.Fixture>
{
    public new class Fixture : DistanceFunctionTests<int>.Fixture
    {
        public override TestStore TestStore => Db2TestStore.Instance;
    }
}

public class Db2IndexKindTests(Db2IndexKindTests.Fixture fixture)
    : IndexKindTests<int>(fixture), IClassFixture<Db2IndexKindTests.Fixture>
{
    public new class Fixture : IndexKindTests<int>.Fixture
    {
        public override TestStore TestStore => Db2TestStore.Instance;
    }
}

public class Db2FilterTests(Db2FilterTests.Fixture fixture)
    : FilterTests<int>(fixture), IClassFixture<Db2FilterTests.Fixture>
{
    public override async Task Not_over_Or()
    {
        // WHERE (NOT (("Int" = 8) OR ("String" = 'foo'))): with a NULL string in the data, SQL's three-valued logic
        // excludes that row, while the LINQ-to-objects reference includes it. Same behavior as other relational providers.
        await Assert.ThrowsAsync<FailException>(() => base.Not_over_Or());

        await this.TestFilterAsync(
            r => r.String != null && !(r.Int == 8 || r.String == "foo"),
            r => r["String"] != null && !((int)r["Int"]! == 8 || r["String"] == "foo"));
    }

    public override async Task NotEqual_with_string()
    {
        // As above: "String" <> 'foo' is not true for NULL.
        await Assert.ThrowsAsync<FailException>(() => base.NotEqual_with_string());

        await this.TestFilterAsync(
            r => r.String != null && r.String != "foo",
            r => r["String"] != null && r["String"] != "foo");
    }

    public new class Fixture : FilterTests<int>.Fixture
    {
        public override TestStore TestStore => Db2TestStore.Instance;
    }
}

public class Db2BasicModelTests(Db2BasicModelTests.Fixture fixture)
    : BasicModelTests<string>(fixture), IClassFixture<Db2BasicModelTests.Fixture>
{
    public new class Fixture : BasicModelTests<string>.Fixture
    {
        public override TestStore TestStore => Db2TestStore.Instance;
    }
}

public class Db2DynamicModelTests(Db2DynamicModelTests.Fixture fixture)
    : DynamicModelTests<string>(fixture), IClassFixture<Db2DynamicModelTests.Fixture>
{
    public new class Fixture : DynamicModelTests<string>.Fixture
    {
        public override TestStore TestStore => Db2TestStore.Instance;
    }
}

public class Db2MultiVectorModelTests(Db2MultiVectorModelTests.Fixture fixture)
    : MultiVectorModelTests<string>(fixture), IClassFixture<Db2MultiVectorModelTests.Fixture>
{
    public new class Fixture : MultiVectorModelTests<string>.Fixture
    {
        public override TestStore TestStore => Db2TestStore.Instance;
    }
}

public class Db2NoDataModelTests(Db2NoDataModelTests.Fixture fixture)
    : NoDataModelTests<string>(fixture), IClassFixture<Db2NoDataModelTests.Fixture>
{
    public new class Fixture : NoDataModelTests<string>.Fixture
    {
        public override TestStore TestStore => Db2TestStore.Instance;
    }
}

public class Db2NoVectorModelTests(Db2NoVectorModelTests.Fixture fixture)
    : NoVectorModelTests<string>(fixture), IClassFixture<Db2NoVectorModelTests.Fixture>
{
    public new class Fixture : NoVectorModelTests<string>.Fixture
    {
        public override TestStore TestStore => Db2TestStore.Instance;
    }
}

public class Db2DataTypeTests(Db2DataTypeTests.Fixture fixture)
    : DataTypeTests<Guid, DataTypeTests<Guid>.DefaultRecord>(fixture), IClassFixture<Db2DataTypeTests.Fixture>
{
    public new class Fixture : DataTypeTests<Guid, DefaultRecord>.Fixture
    {
        public override TestStore TestStore => Db2TestStore.Instance;

        // Db2 has no TINYINT and no time zone aware timestamp.
        public override Type[] UnsupportedDefaultTypes { get; } = [typeof(byte), typeof(DateTimeOffset)];
    }
}

public class Db2EmbeddingTypeTests(Db2EmbeddingTypeTests.Fixture fixture)
    : EmbeddingTypeTests<Guid>(fixture), IClassFixture<Db2EmbeddingTypeTests.Fixture>
{
    public new class Fixture : EmbeddingTypeTests<Guid>.Fixture
    {
        public override TestStore TestStore => Db2TestStore.Instance;
    }
}

public class Db2KeyTypeTests(Db2KeyTypeTests.Fixture fixture)
    : KeyTypeTests(fixture), IClassFixture<Db2KeyTypeTests.Fixture>
{
    [Fact]
    public virtual Task Int() => this.Test(8);

    [Fact]
    public virtual Task Long() => this.Test(8L);

    [Fact]
    public virtual Task String() => this.Test("foo", "bar");

    public new class Fixture : KeyTypeTests.Fixture
    {
        public override TestStore TestStore => Db2TestStore.Instance;
    }
}
