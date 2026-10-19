// Copyright (c) IBM Corporation. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using IBM.Data.Db2;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;

namespace IBM.VectorData.Db2.UnitTests;

public class Db2CommandBuilderTests
{
    private static readonly CollectionModel s_model = new Db2ModelBuilder().Build(typeof(Record), typeof(string), definition: null, defaultEmbeddingGenerator: null);

    [Fact]
    public void CreateTable_MapsTypesAndIndexes()
    {
        using DB2Connection connection = new();

        var commands = Db2CommandBuilder.CreateTable(connection, "MySchema", "My\"Table", s_model);

        Assert.Equal(
            "CREATE TABLE \"MySchema\".\"My\"\"Table\" (\n" +
            "\"Id\" VARCHAR(1000) NOT NULL,\n" +
            "\"Title\" VARCHAR(1000),\n" +
            "\"Count\" INTEGER NOT NULL,\n" +
            "\"Tags\" VARCHAR(32672),\n" +
            "\"Embedding\" VECTOR(4, FLOAT32) NOT NULL,\n" +
            "PRIMARY KEY (\"Id\"))",
            commands[0].CommandText.ReplaceLineEndings("\n"));

        // Only the scalar indexed column gets a B-tree index; the JSON array column does not.
        Assert.Equal(2, commands.Count);
        Assert.Equal("CREATE INDEX \"MySchema\".\"IDX_MyTable_Title\" ON \"MySchema\".\"My\"\"Table\" (\"Title\")", commands[1].CommandText);

        foreach (var command in commands)
        {
            command.Dispose();
        }
    }

    [Fact]
    public void Upsert_MergesWithTypedParametersInColumnOrder()
    {
        using DB2Connection connection = new();
        using DB2Command command = connection.CreateCommand();
        Record record = new() { Id = "k", Title = "t", Count = 2, Tags = ["a"], Embedding = new float[] { 1, 2, 3, 4 } };

        Db2CommandBuilder.Upsert(command, schema: null, "T", s_model, record, getGeneratedEmbedding: null);

        Assert.Equal(
            "MERGE INTO \"T\" AS t\n" +
            "USING (VALUES (CAST(? AS VARCHAR(1000)),CAST(? AS VARCHAR(1000)),CAST(? AS INTEGER),CAST(? AS VARCHAR(32672)),VECTOR(CAST(? AS CLOB), 4, FLOAT32))) AS s (\"Id\",\"Title\",\"Count\",\"Tags\",\"Embedding\")\n" +
            "ON t.\"Id\" = s.\"Id\"\n" +
            "WHEN MATCHED THEN UPDATE SET \"Title\" = s.\"Title\",\"Count\" = s.\"Count\",\"Tags\" = s.\"Tags\",\"Embedding\" = s.\"Embedding\"\n" +
            "WHEN NOT MATCHED THEN INSERT (\"Id\",\"Title\",\"Count\",\"Tags\",\"Embedding\") VALUES (s.\"Id\",s.\"Title\",s.\"Count\",s.\"Tags\",s.\"Embedding\")",
            command.CommandText.ReplaceLineEndings("\n"));

        Assert.Equal(["k", "t", 2, "[\"a\"]", "[1,2,3,4]"], command.Parameters.Cast<DB2Parameter>().Select(p => p.Value));
    }

    [Theory]
    [InlineData(DistanceFunction.CosineDistance, "0 + (1 * \"_db2_distance\")", 0.3)]
    [InlineData(DistanceFunction.CosineSimilarity, "1 + (-1 * \"_db2_distance\")", 0.7)]
    public void SelectVector_ScoresAndThresholdsOnDistance(string distanceFunction, string scoreExpression, double distanceThreshold)
    {
        VectorStoreCollectionDefinition definition = new()
        {
            Properties =
            [
                new VectorStoreKeyProperty("Id", typeof(string)),
                new VectorStoreDataProperty("Count", typeof(int)),
                new VectorStoreVectorProperty("Embedding", typeof(ReadOnlyMemory<float>), 3) { DistanceFunction = distanceFunction },
            ],
        };
        var model = new Db2ModelBuilder().BuildDynamic(definition, defaultEmbeddingGenerator: null);
        using DB2Connection connection = new();

        using DB2Command command = Db2CommandBuilder.SelectVector(
            connection,
            schema: null,
            "T",
            model.VectorProperty,
            model,
            top: 5,
            new VectorSearchOptions<Dictionary<string, object?>> { Skip = 2, ScoreThreshold = 0.3, Filter = r => (int)r["Count"]! > 1 },
            new float[] { 1, 2, 3 });

        Assert.Equal(
            $"SELECT \"Id\",\"Count\", {scoreExpression} AS \"_db2_score\" FROM (\n" +
            "SELECT \"Id\",\"Count\", VECTOR_DISTANCE(\"Embedding\", VECTOR(CAST(? AS CLOB), 3, FLOAT32), COSINE) AS \"_db2_distance\" FROM \"T\" WHERE \"Embedding\" IS NOT NULL AND (\"Count\" > 1)) AS i\n" +
            "WHERE \"_db2_distance\" <= ?\n" +
            "ORDER BY \"_db2_distance\" ASC\n" +
            "OFFSET 2 ROWS FETCH NEXT 5 ROWS ONLY",
            command.CommandText.ReplaceLineEndings("\n"));

        var parameters = command.Parameters.Cast<DB2Parameter>().ToArray();
        Assert.Equal("[1,2,3]", parameters[0].Value);
        Assert.Equal(distanceThreshold, (double)parameters[^1].Value, precision: 10);
    }

    [Fact]
    public void TableMetadataCommands_UseSchemaOrCurrentSchema()
    {
        using DB2Connection connection = new();

        using (var command = Db2CommandBuilder.SelectTableNames(connection, schema: null))
        {
            Assert.Equal("SELECT TABNAME FROM SYSCAT.TABLES WHERE TYPE = 'T' AND TABSCHEMA = COALESCE(CAST(? AS VARCHAR(128)), CURRENT SCHEMA)", command.CommandText);
            Assert.Equal(DBNull.Value, command.Parameters[0].Value);
        }

        using (var command = Db2CommandBuilder.SelectTableName(connection, "S", "T"))
        {
            Assert.EndsWith(" AND TABNAME = ?", command.CommandText);
            Assert.Equal(["S", "T"], command.Parameters.Cast<DB2Parameter>().Select(p => p.Value));
        }

        using (var command = Db2CommandBuilder.DropTableIfExists(connection, null, "T"))
        {
            Assert.Equal("DROP TABLE IF EXISTS \"T\"", command.CommandText);
        }
    }

    [Fact]
    public void KeyCommands_BindKeysInOrder()
    {
        using DB2Connection connection = new();

        using (var command = Db2CommandBuilder.SelectSingle(connection, null, "T", s_model, "k", includeVectors: false))
        {
            Assert.Equal("SELECT \"Id\",\"Title\",\"Count\",\"Tags\" FROM \"T\" WHERE \"Id\" = ?", command.CommandText);
            Assert.Equal("k", command.Parameters[0].Value);
        }

        using (var command = connection.CreateCommand())
        {
            Assert.True(Db2CommandBuilder.SelectMany(command, null, "T", s_model, ["a", "b"], includeVectors: true));
            Assert.Equal("SELECT \"Id\",\"Title\",\"Count\",\"Tags\",\"Embedding\" FROM \"T\" WHERE \"Id\" IN (?,?)", command.CommandText);
        }

        using (var command = connection.CreateCommand())
        {
            Assert.False(Db2CommandBuilder.SelectMany(command, null, "T", s_model, Array.Empty<string>(), includeVectors: true));
        }

        using (var command = Db2CommandBuilder.DeleteSingle(connection, "S", "T", s_model.KeyProperty, "k"))
        {
            Assert.Equal("DELETE FROM \"S\".\"T\" WHERE \"Id\" = ?", command.CommandText);
        }

        using (var command = connection.CreateCommand())
        {
            Assert.True(Db2CommandBuilder.DeleteMany(command, null, "T", s_model.KeyProperty, ["a", "b", "c"]));
            Assert.Equal("DELETE FROM \"T\" WHERE \"Id\" IN (?,?,?)", command.CommandText);
            Assert.Throws<ArgumentNullException>(() => Db2CommandBuilder.DeleteMany(command, null, "T", s_model.KeyProperty, new string[] { null! }));
        }
    }

    [Fact]
    public void SelectWhere_AppliesFilterOrderAndPaging()
    {
        using DB2Connection connection = new();

        using DB2Command command = Db2CommandBuilder.SelectWhere<Record>(
            r => r.Count > 2,
            top: 10,
            new() { Skip = 3, OrderBy = o => o.Descending(r => r.Count).Ascending(r => r.Id) },
            connection,
            null,
            "T",
            s_model);

        Assert.Equal(
            "SELECT \"Id\",\"Title\",\"Count\",\"Tags\" FROM \"T\" WHERE (\"Count\" > 2)\n" +
            "ORDER BY \"Count\" DESC,\"Id\" ASC\n" +
            "OFFSET 3 ROWS FETCH NEXT 10 ROWS ONLY",
            command.CommandText.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Upsert_KeyOnlyModel_HasNoUpdateClause()
    {
        var model = new Db2ModelBuilder().BuildDynamic(new() { Properties = [new VectorStoreKeyProperty("Id", typeof(long))] }, defaultEmbeddingGenerator: null);
        using DB2Connection connection = new();
        using DB2Command command = connection.CreateCommand();

        Db2CommandBuilder.Upsert(command, null, "T", model, new Dictionary<string, object?> { ["Id"] = 5L }, getGeneratedEmbedding: null);

        Assert.DoesNotContain("WHEN MATCHED", command.CommandText);
        Assert.Contains("CAST(? AS BIGINT)", command.CommandText);
    }

    [Fact]
    public void Upsert_UsesGeneratedEmbeddings()
    {
        using DB2Connection connection = new();
        using DB2Command command = connection.CreateCommand();
        Record record = new() { Id = "k", Embedding = [9, 9, 9, 9] };

        Db2CommandBuilder.Upsert(command, null, "T", s_model, record, _ => new Microsoft.Extensions.AI.Embedding<float>(new float[] { 1, 2, 3, 4 }));

        Assert.Equal("[1,2,3,4]", command.Parameters[^1].Value);
    }

    [Fact]
    public void AddParameter_ConvertsValuesForTheDriver()
    {
        using DB2Command command = new();
        var vectorProperty = s_model.VectorProperty;
        Guid guid = Guid.NewGuid();

        command.AddParameter(null, null);
        command.AddParameter(vectorProperty, null);
        command.AddParameter(null, guid);
        command.AddParameter(null, new DateTime(2026, 1, 2));
        command.AddParameter(null, new DateOnly(2026, 1, 2));
        command.AddParameter(null, new TimeOnly(3, 4, 5));
        command.AddParameter(null, new byte[] { 1 });
        command.AddParameter(null, new List<string> { "a" });
        command.AddParameter(null, new ReadOnlyMemory<float>([1, 2]));
        command.AddParameter(null, 42);

        var p = command.Parameters.Cast<DB2Parameter>().ToArray();
        Assert.Equal(DBNull.Value, p[0].Value);
        Assert.Equal((DB2Type.Clob, (object)DBNull.Value), (p[1].DB2Type, p[1].Value));
        Assert.Equal((DB2Type.Char, guid.ToString("D")), (p[2].DB2Type, p[2].Value));
        Assert.Equal(DB2Type.Timestamp, p[3].DB2Type);
        Assert.Equal((DB2Type.Date, new DateTime(2026, 1, 2)), (p[4].DB2Type, p[4].Value));
        Assert.Equal((DB2Type.Time, new TimeSpan(3, 4, 5)), (p[5].DB2Type, p[5].Value));
        Assert.Equal(DB2Type.VarBinary, p[6].DB2Type);
        Assert.Equal("[\"a\"]", p[7].Value);
        Assert.Equal("[1,2]", p[8].Value);
        Assert.Equal(42, p[9].Value);
    }

    [Theory]
    [InlineData(typeof(short), "SMALLINT")]
    [InlineData(typeof(long), "BIGINT")]
    [InlineData(typeof(Guid), "CHAR(36)")]
    [InlineData(typeof(byte[]), "VARBINARY(32672)")]
    [InlineData(typeof(bool), "BOOLEAN")]
    [InlineData(typeof(DateTime), "TIMESTAMP(7)")]
    [InlineData(typeof(DateOnly), "DATE")]
    [InlineData(typeof(TimeOnly), "TIME")]
    [InlineData(typeof(decimal), "DECIMAL(31,8)")]
    [InlineData(typeof(double), "DOUBLE")]
    [InlineData(typeof(float?), "REAL")]
    [InlineData(typeof(string[]), "VARCHAR(32672)")]
    public void Map_DataTypes(Type type, string expected)
    {
        Assert.Equal(expected, Db2CommandBuilder.Map(new DataPropertyModel("P", type)));
    }

    [Fact]
    public void Map_RejectsUnknownType()
    {
        Assert.Throws<NotSupportedException>(() => Db2CommandBuilder.Map(new DataPropertyModel("P", typeof(Uri))));
    }

    [Fact]
    public void QuoteIdentifier_EscapesQuotes()
    {
        Assert.Equal("\"a\"\"b\"", Db2CommandBuilder.QuoteIdentifier("a\"b"));
    }

    [Fact]
    public void DeleteMany_ReturnsFalseForNoKeys()
    {
        using DB2Connection connection = new();
        using DB2Command command = connection.CreateCommand();

        Assert.False(Db2CommandBuilder.DeleteMany(command, null, "T", s_model.KeyProperty, Array.Empty<string>()));
    }

    internal sealed class Record
    {
        [VectorStoreKey]
        public string Id { get; set; } = "";

        [VectorStoreData(IsIndexed = true)]
        public string? Title { get; set; }

        [VectorStoreData]
        public int Count { get; set; }

        [VectorStoreData(IsIndexed = true)]
        public List<string>? Tags { get; set; }

        [VectorStoreVector(4)]
        public float[] Embedding { get; set; } = [];
    }
}
