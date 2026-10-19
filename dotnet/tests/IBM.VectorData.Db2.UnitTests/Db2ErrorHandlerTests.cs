// Copyright (c) IBM Corporation. All rights reserved.

using System;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Extensions.VectorData;

namespace IBM.VectorData.Db2.UnitTests;

public class Db2ErrorHandlerTests
{
    private static readonly VectorStoreCollectionMetadata s_metadata = new()
    {
        VectorStoreSystemName = "ibm.db2",
        VectorStoreName = "DB",
        CollectionName = "c",
    };

    [Fact]
    public async Task DbException_IsWrappedAsync()
    {
        TestDbException inner = new();

        var ex = await Assert.ThrowsAsync<VectorStoreException>(() => Db2ErrorHandler.RunAsync(s_metadata, "Op", () => Task.FromException(inner)));
        Assert.Same(inner, ex.InnerException);
        Assert.Equal("Op", ex.OperationName);
        Assert.Equal("c", ex.CollectionName);

        ex = await Assert.ThrowsAsync<VectorStoreException>(() => Db2ErrorHandler.RunAsync(s_metadata, "Op2", () => Task.FromException<int>(inner)));
        Assert.Equal("Op2", ex.OperationName);
    }

    [Fact]
    public async Task OtherExceptions_PassThroughAsync()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Db2ErrorHandler.RunAsync(s_metadata, "Op", () => throw new InvalidOperationException()));
        Assert.Equal(42, await Db2ErrorHandler.RunAsync(s_metadata, "Op", () => Task.FromResult(42)));
    }

    private sealed class TestDbException : DbException;
}
