// Copyright (c) IBM Corporation. All rights reserved.

using System;
using IBM.Data.Db2.Vector;
using Microsoft.Extensions.VectorData;

namespace IBM.VectorData.Db2;

/// <summary>
/// How a MEVD <see cref="DistanceFunction"/> maps onto Db2 <c>VECTOR_DISTANCE</c>.
/// </summary>
/// <remarks>
/// Db2 always returns a distance (lower is closer), so search always orders by the raw distance ascending.
/// The score reported to the caller is <c>ScoreOffset + (ScoreSign * distance)</c>, which turns the distance into
/// a similarity for the similarity-based functions.
/// </remarks>
internal readonly record struct Db2DistanceFunction(Db2VectorDistanceMetric Metric, int ScoreSign, int ScoreOffset)
{
    /// <summary>Gets a value indicating whether a higher score means a closer match.</summary>
    public bool IsSimilarity => this.ScoreSign < 0;
}

internal static class Db2DistanceFunctions
{
    internal const string Default = DistanceFunction.CosineDistance;

    internal static Db2DistanceFunction Get(string? distanceFunction) => (distanceFunction ?? Default) switch
    {
        DistanceFunction.CosineDistance => new(Db2VectorDistanceMetric.Cosine, 1, 0),
        DistanceFunction.CosineSimilarity => new(Db2VectorDistanceMetric.Cosine, -1, 1),
        DistanceFunction.NegativeDotProductSimilarity => new(Db2VectorDistanceMetric.Dot, 1, 0),
        DistanceFunction.DotProductSimilarity => new(Db2VectorDistanceMetric.Dot, -1, 0),
        DistanceFunction.EuclideanDistance => new(Db2VectorDistanceMetric.Euclidean, 1, 0),
        DistanceFunction.EuclideanSquaredDistance => new(Db2VectorDistanceMetric.EuclideanSquared, 1, 0),
        DistanceFunction.HammingDistance => new(Db2VectorDistanceMetric.Hamming, 1, 0),
        DistanceFunction.ManhattanDistance => new(Db2VectorDistanceMetric.Manhattan, 1, 0),
        var name => throw new NotSupportedException($"Distance function '{name}' is not supported by the Db2 provider."),
    };
}
