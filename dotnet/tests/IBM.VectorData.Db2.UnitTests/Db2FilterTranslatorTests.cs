// Copyright (c) IBM Corporation. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;

namespace IBM.VectorData.Db2.UnitTests;

public class Db2FilterTranslatorTests
{
    private static readonly CollectionModel s_model = new Db2ModelBuilder().Build(typeof(Record), typeof(int), definition: null, defaultEmbeddingGenerator: null);

    [Theory]
    [InlineData(8)]
    public void Comparison_ParameterizesCapturedValues(int value)
    {
        var (sql, parameters) = Translate(r => r.Amount > value && r.Text == "foo");

        Assert.Equal("((\"Amount\" > ?) AND (\"Text\" = 'foo'))", sql);
        Assert.Equal([8], parameters);
    }

    [Fact]
    public void StringConstant_IsEscaped()
    {
        var (sql, _) = Translate(r => r.Text == "it's");

        Assert.Equal("(\"Text\" = 'it''s')", sql);
    }

    [Theory]
    [InlineData(null)]
    public void NullComparison_UsesIsNull(string? value)
    {
        Assert.Equal("(\"Text\" IS NULL)", Translate(r => r.Text == null).Sql);
        Assert.Equal("(\"Text\" IS NOT NULL)", Translate(r => r.Text != value).Sql);
    }

    [Fact]
    public void BareBoolean_BecomesComparison()
    {
        Assert.Equal("\"Flag\" = TRUE", Translate(r => r.Flag).Sql);
        Assert.Equal("(NOT \"Flag\" = TRUE)", Translate(r => !r.Flag).Sql);
    }

    [Fact]
    public void ContainsOverCapturedArray_InlinesEscapedValues()
    {
        string[] values = ["a", "b'c"];

        var (sql, parameters) = Translate(r => values.Contains(r.Text));

        Assert.Equal("(\"Text\" IN ('a', 'b''c'))", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void ContainsOverEmptyArray_MatchesNothing()
    {
        string[] values = [];

        Assert.Equal("1 = 0", Translate(r => values.Contains(r.Text)).Sql);
    }

    [Fact]
    public void ContainsOverArrayColumn_SearchesSerializedElement()
    {
        var (sql, parameters) = Translate(r => r.Tags.Contains("x,\"y"));

        Assert.Equal(
            "((LOCATE(',' || CAST(? AS VARCHAR(32672)) || ',', ',' || SUBSTR(\"Tags\", 2, LENGTH(\"Tags\") - 2) || ',') > 0))",
            sql);
        Assert.Equal(["\"x,\\\"y\""], parameters);
    }

    [Fact]
    public void AnyOverArrayColumn_OrsEachValue()
    {
        string[] values = ["a", "b"];

        var (sql, parameters) = Translate(r => r.Tags.Any(t => values.Contains(t)));

        Assert.StartsWith("((LOCATE(", sql);
        Assert.Contains(") OR (LOCATE(", sql);
        Assert.Equal(["\"a\"", "\"b\""], parameters);
    }

    [Fact]
    public void FloatingPointConstants_UseInvariantCulture()
    {
        var (sql, _) = Translate(r => r.Number > 1.5);

        Assert.Equal("(\"Number\" > 1.5E0)", sql);
    }

    [Fact]
    public void DateTimeConstant_UsesTimestampLiteral()
    {
        var (sql, _) = Translate(r => r.When == new DateTime(2026, 10, 19, 1, 2, 3, 456));

        Assert.Equal("(\"When\" = TIMESTAMP '2026-10-19 01:02:03.4560000')", sql);
    }

    [Fact]
    public void CapturedArrayElements_UseDb2Literals()
    {
        DateOnly[] days = [new(2026, 10, 19)];
        TimeOnly[] times = [new(12, 30, 45)];
        Guid[] ids = [new("603840bf-cf91-4521-8b8e-8b6a2e75910a")];
        decimal[] prices = [1.5m];
        long[] bigs = [9L];
        float[] ratios = [0.25f];
        bool[] flags = [true];
        string?[] texts = [null];

        Assert.Equal("(\"Day\" IN (DATE '2026-10-19'))", Translate(r => days.Contains(r.Day)).Sql);
        Assert.Equal("(\"Time\" IN (TIME '12:30:45'))", Translate(r => times.Contains(r.Time)).Sql);
        Assert.Equal("(\"Id\" IN ('603840bf-cf91-4521-8b8e-8b6a2e75910a'))", Translate(r => ids.Contains(r.Id)).Sql);
        Assert.Equal("(\"Price\" IN (1.5))", Translate(r => prices.Contains(r.Price)).Sql);
        Assert.Equal("(\"Big\" IN (9))", Translate(r => bigs.Contains(r.Big)).Sql);
        Assert.Equal("(\"Ratio\" IN (0.25E0))", Translate(r => ratios.Contains(r.Ratio)).Sql);
        Assert.Equal("(\"Flag\" IN (TRUE))", Translate(r => flags.Contains(r.Flag)).Sql);
        Assert.Equal("(\"Text\" IN (NULL))", Translate(r => texts.Contains(r.Text)).Sql);
        Assert.Equal("1 = 1", Translate(r => true).Sql);
    }

    [Fact]
    public void ArrayEquality_ParameterizesCapturedArray()
    {
        string[] tags = ["a", "it's"];

        var (sql, parameters) = Translate(r => r.Tags == tags);

        Assert.Equal("(\"Tags\" = ?)", sql);
        Assert.Same(tags, parameters[0]);
    }

    [Fact]
    public void ContainsOverInlineArray_TranslatesElements()
    {
        Assert.Equal("(\"Amount\" IN (1, 2))", Translate(r => new[] { 1, 2 }.Contains(r.Amount)).Sql);
    }

    [Fact]
    public void AnyOverArrayColumn_WithInlineValuesAndList()
    {
        var (_, parameters) = Translate(r => r.List.Any(t => new[] { "x" }.Contains(t)));
        Assert.Equal(["\"x\""], parameters);

        string[] empty = [];
        Assert.Equal("1 = 0", Translate(r => r.Tags.Any(t => empty.Contains(t))).Sql);
    }

    [Fact]
    public void NullableAndDynamicAccess_AreTranslated()
    {
        int? value = 3;
        Assert.Equal("(\"Amount\" = ?)", Translate(r => r.Amount == value).Sql);

        VectorStoreCollectionDefinition definition = new()
        {
            Properties = [new VectorStoreKeyProperty("Key", typeof(int)), new VectorStoreDataProperty("Count", typeof(int))],
        };
        var model = new Db2ModelBuilder().BuildDynamic(definition, defaultEmbeddingGenerator: null);
        StringBuilder sb = new();
        Expression<Func<Dictionary<string, object?>, bool>> filter = r => (int)r["Count"]! > 1;
        new Db2FilterTranslator(model, filter, sb).Translate();
        Assert.Equal("(\"Count\" > 1)", sb.ToString());
    }

    [Fact]
    public void NotOverOrAndNotEqual_AreTranslated()
    {
        Assert.Equal("(NOT ((\"Amount\" = 1) OR (\"Amount\" = 2)))", Translate(r => !(r.Amount == 1 || r.Amount == 2)).Sql);
        Assert.Equal("(\"Amount\" <> 1)", Translate(r => !(r.Amount == 1)).Sql);
        Assert.Equal("(\"Amount\" = 1)", Translate(r => !(r.Amount != 1)).Sql);
        Assert.Equal("(\"Text\" IS NULL)", Translate(r => null == r.Text).Sql);
        Assert.Equal("(\"Text\" IS NOT NULL)", Translate(r => null != r.Text).Sql);
    }

    [Fact]
    public void UnsupportedExpressions_Throw()
    {
        Assert.Throws<NotSupportedException>(() => Translate(r => r.Text!.StartsWith("a", StringComparison.Ordinal)));
        Assert.Throws<NotSupportedException>(() => Translate(r => r.Amount + 1 > 2));
        Assert.Throws<NotSupportedException>(() => Translate(r => r.Tags.Contains(r.Text!)));
        Assert.Throws<NotSupportedException>(() => Translate(r => r.Tags.Any(t => t == "a")));
        Assert.Throws<NotSupportedException>(() => Translate(r => -r.Amount > 1));
    }

    private static (string Sql, List<object?> Parameters) Translate(Expression<Func<Record, bool>> filter)
    {
        StringBuilder sb = new();
        Db2FilterTranslator translator = new(s_model, filter, sb);
        translator.Translate();
        return (sb.ToString(), translator.ParameterValues);
    }

#pragma warning disable CA1812 // Only used as a model for filter translation, never instantiated.
    internal sealed class Record
#pragma warning restore CA1812
    {
        [VectorStoreKey]
        public int Key { get; set; }

        [VectorStoreData]
        public int Amount { get; set; }

        [VectorStoreData]
        public double Number { get; set; }

        [VectorStoreData]
        public string? Text { get; set; }

        [VectorStoreData]
        public bool Flag { get; set; }

        [VectorStoreData]
        public DateTime When { get; set; }

        [VectorStoreData]
        public string[] Tags { get; set; } = [];

        [VectorStoreData]
        public List<string> List { get; set; } = [];

        [VectorStoreData]
        public DateOnly Day { get; set; }

        [VectorStoreData]
        public TimeOnly Time { get; set; }

        [VectorStoreData]
        public Guid Id { get; set; }

        [VectorStoreData]
        public decimal Price { get; set; }

        [VectorStoreData]
        public long Big { get; set; }

        [VectorStoreData]
        public float Ratio { get; set; }


        [VectorStoreVector(3)]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }
}
