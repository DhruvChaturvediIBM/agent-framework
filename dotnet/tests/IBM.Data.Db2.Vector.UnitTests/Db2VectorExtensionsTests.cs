// Copyright (c) IBM Corporation. All rights reserved.

using System;
using System.Data;

namespace IBM.Data.Db2.Vector.UnitTests;

public class Db2VectorExtensionsTests
{
    [Fact]
    public void AddVectorParameter_BindsTextAsClob()
    {
        using DB2Command command = new();

        var parameter = command.AddVectorParameter(new float[] { 1, 2.5f });

        Assert.Same(parameter, command.Parameters[0]);
        Assert.Equal(DB2Type.Clob, parameter.DB2Type);
        Assert.Equal("[1,2.5]", parameter.Value);
    }

    [Fact]
    public void AddVectorParameter_BindsNull()
    {
        using DB2Command command = new();

        Assert.Equal(DBNull.Value, command.AddVectorParameter(null).Value);
    }

    [Fact]
    public void GetVector_ParsesColumnText()
    {
        using DataTable table = new();
        table.Columns.Add("V", typeof(string));
        table.Rows.Add("[1,2,3]");
        using var reader = table.CreateDataReader();
        reader.Read();

        Assert.Equal([1f, 2f, 3f], reader.GetVector(0));
    }

    [Fact]
    public void Arguments_AreValidated()
    {
        Assert.Throws<ArgumentNullException>(() => Db2VectorExtensions.AddVectorParameter(null!, null));
        Assert.Throws<ArgumentNullException>(() => Db2VectorExtensions.GetVector(null!, 0));
        Assert.Throws<ArgumentNullException>(() => Db2VectorText.Parse(null!));
        Assert.Throws<ArgumentException>(() => Db2VectorSql.Distance(" ", "x", Db2VectorDistanceMetric.Cosine));
        Assert.Throws<ArgumentOutOfRangeException>(() => Db2VectorSql.GetKeyword((Db2VectorElementType)42));
    }
}
