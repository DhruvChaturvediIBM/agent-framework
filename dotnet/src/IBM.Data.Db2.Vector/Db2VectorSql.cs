// Copyright (c) IBM Corporation. All rights reserved.

using System;
using System.Globalization;
using Microsoft.Shared.Diagnostics;

namespace IBM.Data.Db2.Vector;

/// <summary>
/// Produces the SQL fragments needed to work with Db2 <c>VECTOR</c> values.
/// </summary>
/// <remarks>
/// The Db2 .NET driver has no parameter type for <c>VECTOR</c>. Vectors are bound as text
/// (see <see cref="Db2VectorText"/>) and converted on the server with <see cref="VectorFromParameter"/>.
/// </remarks>
public static class Db2VectorSql
{
    /// <summary>Gets the Db2 keyword for a distance metric, as used by <c>VECTOR_DISTANCE</c> and <c>CREATE VECTOR INDEX</c>.</summary>
    /// <param name="metric">The distance metric.</param>
    /// <returns>The SQL keyword.</returns>
    public static string GetKeyword(Db2VectorDistanceMetric metric) => metric switch
    {
        Db2VectorDistanceMetric.Euclidean => "EUCLIDEAN",
        Db2VectorDistanceMetric.EuclideanSquared => "EUCLIDEAN_SQUARED",
        Db2VectorDistanceMetric.Cosine => "COSINE",
        Db2VectorDistanceMetric.Dot => "DOT",
        Db2VectorDistanceMetric.Hamming => "HAMMING",
        Db2VectorDistanceMetric.Manhattan => "MANHATTAN",
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, null),
    };

    /// <summary>Gets the Db2 keyword for a vector coordinate type.</summary>
    /// <param name="elementType">The coordinate type.</param>
    /// <returns>The SQL keyword.</returns>
    public static string GetKeyword(Db2VectorElementType elementType) => elementType switch
    {
        Db2VectorElementType.Float32 => "FLOAT32",
        Db2VectorElementType.Int8 => "INT8",
        _ => throw new ArgumentOutOfRangeException(nameof(elementType), elementType, null),
    };

    /// <summary>Gets the column type for a vector, for example <c>VECTOR(1536, FLOAT32)</c>.</summary>
    /// <param name="dimensions">The number of dimensions.</param>
    /// <param name="elementType">The coordinate type.</param>
    /// <returns>The SQL column type.</returns>
    public static string ColumnType(int dimensions, Db2VectorElementType elementType = Db2VectorElementType.Float32)
    {
        Throw.IfLessThan(dimensions, 1);

        return string.Create(CultureInfo.InvariantCulture, $"VECTOR({dimensions}, {GetKeyword(elementType)})");
    }

    /// <summary>
    /// Gets an expression that converts a text parameter marker into a vector,
    /// for example <c>VECTOR(CAST(? AS CLOB), 1536, FLOAT32)</c>.
    /// </summary>
    /// <param name="dimensions">The number of dimensions.</param>
    /// <param name="elementType">The coordinate type.</param>
    /// <returns>The SQL expression containing a single <c>?</c> parameter marker.</returns>
    public static string VectorFromParameter(int dimensions, Db2VectorElementType elementType = Db2VectorElementType.Float32)
    {
        Throw.IfLessThan(dimensions, 1);

        return string.Create(CultureInfo.InvariantCulture, $"VECTOR(CAST(? AS CLOB), {dimensions}, {GetKeyword(elementType)})");
    }

    /// <summary>Gets a <c>VECTOR_DISTANCE</c> call over two vector expressions.</summary>
    /// <param name="left">The first vector expression, typically a quoted column name.</param>
    /// <param name="right">The second vector expression, typically from <see cref="VectorFromParameter"/>.</param>
    /// <param name="metric">The distance metric.</param>
    /// <returns>The SQL expression.</returns>
    public static string Distance(string left, string right, Db2VectorDistanceMetric metric)
    {
        Throw.IfNullOrWhitespace(left);
        Throw.IfNullOrWhitespace(right);

        return $"VECTOR_DISTANCE({left}, {right}, {GetKeyword(metric)})";
    }
}
