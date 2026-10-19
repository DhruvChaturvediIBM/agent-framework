// Copyright (c) IBM Corporation. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace IBM.VectorData.Db2.UnitTests;

internal static class TestSupport
{
    // An invalid port makes the driver fail on open immediately, without network access.
    internal const string UnreachableConnectionString = "Server=127.0.0.1:99999;Database=TESTDB;UID=user;PWD=password;";
}

public sealed class Hotel
{
    [VectorStoreKey]
    public int Id { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string? Name { get; set; }

    [VectorStoreVector(3)]
    public ReadOnlyMemory<float>? Embedding { get; set; }
}

public sealed class GuidHotel
{
    [VectorStoreKey]
    public Guid Id { get; set; }

    [VectorStoreData]
    public string Description { get; set; } = "";

    [VectorStoreVector(3)]
    public string? Embedding { get; set; }
}

/// <summary>Returns a fixed embedding and records the inputs it was asked to embed.</summary>
internal sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public List<string> Inputs { get; } = [];

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var inputs = values.ToList();
        this.Inputs.AddRange(inputs);
        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(inputs.Select(_ => new Embedding<float>(new float[] { 1, 2, 3 }))));
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
