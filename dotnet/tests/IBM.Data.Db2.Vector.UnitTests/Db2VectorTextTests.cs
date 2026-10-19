// Copyright (c) IBM Corporation. All rights reserved.

using System;

namespace IBM.Data.Db2.Vector.UnitTests;

public class Db2VectorTextTests
{
    [Fact]
    public void Format_UsesInvariantCultureAndNoSpaces()
    {
        Assert.Equal("[1,2.5,-3,0.001]", Db2VectorText.Format([1f, 2.5f, -3f, 0.001f]));
    }

    [Fact]
    public void Format_RoundTripsThroughParse()
    {
        float[] vector = [0.1f, -1.17549435E-38f, 3.4028235E+38f, 1e-7f, 123456.789f];

        Assert.Equal(vector, Db2VectorText.Parse(Db2VectorText.Format(vector)));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Format_RejectsNonFiniteValues(float value)
    {
        Assert.Throws<ArgumentException>(() => Db2VectorText.Format([1f, value]));
    }

    [Fact]
    public void Format_RejectsEmptyVector()
    {
        Assert.Throws<ArgumentException>(() => Db2VectorText.Format([]));
    }

    [Theory]
    [InlineData("[1,2,3]")]
    [InlineData(" [1, 2 ,3] ")]
    [InlineData("[1E0,2.0,3e+00]")]
    public void Parse_AcceptsDb2Output(string text)
    {
        Assert.Equal([1f, 2f, 3f], Db2VectorText.Parse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1,2,3")]
    [InlineData("[1,2,3")]
    [InlineData("[]")]
    [InlineData("[ ]")]
    [InlineData("[1,,3]")]
    [InlineData("[1,a,3]")]
    public void Parse_RejectsInvalidText(string text)
    {
        Assert.Throws<FormatException>(() => Db2VectorText.Parse(text));
    }
}
