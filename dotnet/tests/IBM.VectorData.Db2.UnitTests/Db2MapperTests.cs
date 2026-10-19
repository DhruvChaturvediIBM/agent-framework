// Copyright (c) IBM Corporation. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;

namespace IBM.VectorData.Db2.UnitTests;

public class Db2MapperTests
{
    private static readonly Guid s_guid = new("603840bf-cf91-4521-8b8e-8b6a2e75910a");

    [Fact]
    public void MapsEveryDataType()
    {
        var model = new Db2ModelBuilder().Build(typeof(AllTypes), typeof(string), definition: null, defaultEmbeddingGenerator: null);
        using DataTable table = CreateTable(model);
        table.Rows.Add(
            "k", (short)1, 2, 3L, 4.5f, 5.5d, 6.5m, "text", true, s_guid.ToString(), new byte[] { 1, 2 },
            new DateTime(2026, 10, 19, 1, 2, 3), new DateTime(2026, 10, 19), new TimeSpan(12, 30, 45),
            "[\"a\",\"b\"]", "[\"c\"]", 7, "[1,2,3]", "[4,5,6]", "[7,8,9]");

        using var reader = table.CreateDataReader();
        reader.Read();
        var record = new Db2Mapper<AllTypes>(model).MapFromStorageToDataModel(reader, includeVectors: true);

        Assert.Equal("k", record.Key);
        Assert.Equal((short)1, record.Short);
        Assert.Equal(2, record.Int);
        Assert.Equal(3L, record.Long);
        Assert.Equal(4.5f, record.Float);
        Assert.Equal(5.5d, record.Double);
        Assert.Equal(6.5m, record.Decimal);
        Assert.Equal("text", record.String);
        Assert.True(record.Bool);
        Assert.Equal(s_guid, record.Guid);
        Assert.Equal(new byte[] { 1, 2 }, record.Bytes);
        Assert.Equal(new DateTime(2026, 10, 19, 1, 2, 3), record.DateTime);
        Assert.Equal(new DateOnly(2026, 10, 19), record.DateOnly);
        Assert.Equal(new TimeOnly(12, 30, 45), record.TimeOnly);
        Assert.Equal(["a", "b"], record.StringArray!);
        Assert.Equal(["c"], record.StringList!);
        Assert.Equal(7, record.NullableInt);
        Assert.Equal(new float[] { 1, 2, 3 }, record.Memory.ToArray());
        Assert.Equal(new float[] { 4, 5, 6 }, record.Embedding!.Vector.ToArray());
        Assert.Equal(new float[] { 7, 8, 9 }, record.Array);
    }

    [Fact]
    public void MapsNullsAndSkipsVectorsWhenNotRequested()
    {
        var model = new Db2ModelBuilder().Build(typeof(AllTypes), typeof(string), definition: null, defaultEmbeddingGenerator: null);
        using DataTable table = CreateTable(model);
        var row = table.NewRow();
        row["Key"] = "k";
        row["Memory"] = "[1,2,3]";
        table.Rows.Add(row);

        using var reader = table.CreateDataReader();
        reader.Read();
        var record = new Db2Mapper<AllTypes>(model).MapFromStorageToDataModel(reader, includeVectors: false);

        Assert.Null(record.String);
        Assert.Null(record.NullableInt);
        Assert.Null(record.StringArray);
        Assert.True(record.Memory.IsEmpty);
    }

    [Fact]
    public void MapsDynamicRecords()
    {
        VectorStoreCollectionDefinition definition = new()
        {
            Properties =
            [
                new VectorStoreKeyProperty("Key", typeof(int)),
                new VectorStoreDataProperty("Text", typeof(string)),
                new VectorStoreVectorProperty("Vector", typeof(ReadOnlyMemory<float>), 2),
            ],
        };
        var model = new Db2ModelBuilder().BuildDynamic(definition, defaultEmbeddingGenerator: null);
        using DataTable table = CreateTable(model);
        table.Rows.Add(5, "hello", "[1,2]");

        using var reader = table.CreateDataReader();
        reader.Read();
        var record = new Db2Mapper<Dictionary<string, object?>>(model).MapFromStorageToDataModel(reader, includeVectors: true);

        Assert.Equal(5, record["Key"]);
        Assert.Equal("hello", record["Text"]);
        Assert.Equal(new float[] { 1, 2 }, ((ReadOnlyMemory<float>)record["Vector"]!).ToArray());
    }

    [Fact]
    public void InvalidValues_ReportTheProperty()
    {
        var model = new Db2ModelBuilder().Build(typeof(AllTypes), typeof(string), definition: null, defaultEmbeddingGenerator: null);
        var mapper = new Db2Mapper<AllTypes>(model);

        using DataTable badData = CreateTable(model);
        var row = badData.NewRow();
        row["Key"] = "k";
        row["Guid"] = "not a guid";
        badData.Rows.Add(row);
        using (var reader = badData.CreateDataReader())
        {
            reader.Read();
            var ex = Assert.Throws<InvalidOperationException>(() => mapper.MapFromStorageToDataModel(reader, includeVectors: false));
            Assert.Contains("'Guid'", ex.Message);
        }

        using DataTable badVector = CreateTable(model);
        row = badVector.NewRow();
        row["Key"] = "k";
        row["Memory"] = "not a vector";
        badVector.Rows.Add(row);
        using (var reader = badVector.CreateDataReader())
        {
            reader.Read();
            var ex = Assert.Throws<InvalidOperationException>(() => mapper.MapFromStorageToDataModel(reader, includeVectors: true));
            Assert.Contains("'Memory'", ex.Message);
        }
    }

    /// <summary>Creates a table shaped like the Db2 driver's results: vectors as text, GUIDs as text, TIME as TimeSpan.</summary>
    private static DataTable CreateTable(CollectionModel model)
    {
        DataTable table = new();
        foreach (var property in model.Properties)
        {
            Type type = Nullable.GetUnderlyingType(property.Type) ?? property.Type;
            type = property is VectorPropertyModel || type == typeof(Guid) || type == typeof(string[]) || type == typeof(List<string>) ? typeof(string)
                : type == typeof(DateOnly) ? typeof(DateTime)
                : type == typeof(TimeOnly) ? typeof(TimeSpan)
                : type;
            table.Columns.Add(property.StorageName, type);
        }

        return table;
    }

    public sealed class AllTypes
    {
        [VectorStoreKey] public string Key { get; set; } = "";
        [VectorStoreData] public short Short { get; set; }
        [VectorStoreData] public int Int { get; set; }
        [VectorStoreData] public long Long { get; set; }
        [VectorStoreData] public float Float { get; set; }
        [VectorStoreData] public double Double { get; set; }
        [VectorStoreData] public decimal Decimal { get; set; }
        [VectorStoreData] public string? String { get; set; }
        [VectorStoreData] public bool Bool { get; set; }
        [VectorStoreData] public Guid Guid { get; set; }
        [VectorStoreData] public byte[]? Bytes { get; set; }
        [VectorStoreData] public DateTime DateTime { get; set; }
        [VectorStoreData] public DateOnly DateOnly { get; set; }
        [VectorStoreData] public TimeOnly TimeOnly { get; set; }
        [VectorStoreData] public string[]? StringArray { get; set; }
        [VectorStoreData] public List<string>? StringList { get; set; }
        [VectorStoreData] public int? NullableInt { get; set; }
        [VectorStoreVector(3)] public ReadOnlyMemory<float> Memory { get; set; }
        [VectorStoreVector(3)] public Embedding<float>? Embedding { get; set; }
        [VectorStoreVector(3)] public float[]? Array { get; set; }
    }
}
