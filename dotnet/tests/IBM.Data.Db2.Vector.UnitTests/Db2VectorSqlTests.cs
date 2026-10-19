// Copyright (c) IBM Corporation. All rights reserved.

using System;

namespace IBM.Data.Db2.Vector.UnitTests;

public class Db2VectorSqlTests
{
    [Theory]
    [InlineData(Db2VectorDistanceMetric.Euclidean, "EUCLIDEAN")]
    [InlineData(Db2VectorDistanceMetric.EuclideanSquared, "EUCLIDEAN_SQUARED")]
    [InlineData(Db2VectorDistanceMetric.Cosine, "COSINE")]
    [InlineData(Db2VectorDistanceMetric.Dot, "DOT")]
    [InlineData(Db2VectorDistanceMetric.Hamming, "HAMMING")]
    [InlineData(Db2VectorDistanceMetric.Manhattan, "MANHATTAN")]
    public void GetKeyword_MapsEveryMetric(Db2VectorDistanceMetric metric, string expected)
    {
        Assert.Equal(expected, Db2VectorSql.GetKeyword(metric));
    }

    [Fact]
    public void GetKeyword_RejectsUndefinedMetric()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Db2VectorSql.GetKeyword((Db2VectorDistanceMetric)42));
    }

    [Fact]
    public void ColumnType_IncludesDimensionsAndElementType()
    {
        Assert.Equal("VECTOR(1536, FLOAT32)", Db2VectorSql.ColumnType(1536));
        Assert.Equal("VECTOR(8, INT8)", Db2VectorSql.ColumnType(8, Db2VectorElementType.Int8));
    }

    [Fact]
    public void VectorFromParameter_CastsTextParameter()
    {
        Assert.Equal("VECTOR(CAST(? AS CLOB), 3, FLOAT32)", Db2VectorSql.VectorFromParameter(3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Dimensions_MustBePositive(int dimensions)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Db2VectorSql.ColumnType(dimensions));
        Assert.Throws<ArgumentOutOfRangeException>(() => Db2VectorSql.VectorFromParameter(dimensions));
    }

    [Fact]
    public void Distance_ComposesExpressions()
    {
        Assert.Equal(
            "VECTOR_DISTANCE(\"Embedding\", VECTOR(CAST(? AS CLOB), 3, FLOAT32), COSINE)",
            Db2VectorSql.Distance("\"Embedding\"", Db2VectorSql.VectorFromParameter(3), Db2VectorDistanceMetric.Cosine));
    }
}
