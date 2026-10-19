// Copyright (c) IBM Corporation. All rights reserved.

using System;
using IBM.Data.Db2.Vector;
using Microsoft.Extensions.VectorData;

namespace IBM.VectorData.Db2.UnitTests;

public class Db2DistanceFunctionsTests
{
    [Theory]
    [InlineData(DistanceFunction.CosineDistance, Db2VectorDistanceMetric.Cosine, false)]
    [InlineData(DistanceFunction.CosineSimilarity, Db2VectorDistanceMetric.Cosine, true)]
    [InlineData(DistanceFunction.NegativeDotProductSimilarity, Db2VectorDistanceMetric.Dot, false)]
    [InlineData(DistanceFunction.DotProductSimilarity, Db2VectorDistanceMetric.Dot, true)]
    [InlineData(DistanceFunction.EuclideanDistance, Db2VectorDistanceMetric.Euclidean, false)]
    [InlineData(DistanceFunction.EuclideanSquaredDistance, Db2VectorDistanceMetric.EuclideanSquared, false)]
    [InlineData(DistanceFunction.HammingDistance, Db2VectorDistanceMetric.Hamming, false)]
    [InlineData(DistanceFunction.ManhattanDistance, Db2VectorDistanceMetric.Manhattan, false)]
    public void Get_MapsEveryDistanceFunction(string distanceFunction, Db2VectorDistanceMetric metric, bool isSimilarity)
    {
        var mapped = Db2DistanceFunctions.Get(distanceFunction);

        Assert.Equal(metric, mapped.Metric);
        Assert.Equal(isSimilarity, mapped.IsSimilarity);
    }

    [Theory]
    // Db2 returns distances: COSINE is 1 - cosine similarity, DOT is the negated dot product.
    [InlineData(DistanceFunction.CosineDistance, 0.25, 0.25)]
    [InlineData(DistanceFunction.CosineSimilarity, 0.25, 0.75)]
    [InlineData(DistanceFunction.NegativeDotProductSimilarity, -17, -17)]
    [InlineData(DistanceFunction.DotProductSimilarity, -17, 17)]
    [InlineData(DistanceFunction.EuclideanDistance, 3, 3)]
    public void Score_IsDerivedFromDb2Distance(string distanceFunction, double db2Distance, double expectedScore)
    {
        var mapped = Db2DistanceFunctions.Get(distanceFunction);

        Assert.Equal(expectedScore, mapped.ScoreOffset + (mapped.ScoreSign * db2Distance));
    }

    [Fact]
    public void Get_DefaultsToCosineDistance()
    {
        Assert.Equal(Db2DistanceFunctions.Get(DistanceFunction.CosineDistance), Db2DistanceFunctions.Get(null));
    }

    [Fact]
    public void Get_RejectsUnknownFunction()
    {
        Assert.Throws<NotSupportedException>(() => Db2DistanceFunctions.Get("Chebyshev"));
    }
}
