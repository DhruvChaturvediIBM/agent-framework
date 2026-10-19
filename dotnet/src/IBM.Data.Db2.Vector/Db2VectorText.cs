// Copyright (c) IBM Corporation. All rights reserved.

using System;
using System.Globalization;
using System.Text;

namespace IBM.Data.Db2.Vector;

/// <summary>
/// Converts vectors to and from the text form Db2 uses for <c>VECTOR</c> values, for example <c>[1,2.5,-3]</c>.
/// </summary>
/// <remarks>
/// The Db2 .NET driver returns <c>VECTOR</c> columns as this text, and vectors are bound as this text
/// before being converted with <see cref="Db2VectorSql.VectorFromParameter"/>.
/// </remarks>
public static class Db2VectorText
{
    /// <summary>Formats a vector as Db2 vector text.</summary>
    /// <param name="vector">The vector coordinates.</param>
    /// <returns>The vector text.</returns>
    /// <exception cref="ArgumentException">The vector is empty or contains NaN or infinity, which Db2 cannot store.</exception>
    public static string Format(ReadOnlySpan<float> vector)
    {
        if (vector.IsEmpty)
        {
            throw new ArgumentException("A Db2 vector must have at least one dimension.", nameof(vector));
        }

        // Shortest round-trippable form is at most 15 characters per coordinate, plus the separator.
        StringBuilder sb = new(2 + (vector.Length * 16));
        sb.Append('[');
        for (int i = 0; i < vector.Length; i++)
        {
            float value = vector[i];
            if (!float.IsFinite(value))
            {
                throw new ArgumentException($"Coordinate {i} is {value}; Db2 vectors only support finite values.", nameof(vector));
            }

            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        return sb.Append(']').ToString();
    }

    /// <summary>Parses Db2 vector text.</summary>
    /// <param name="text">The vector text, for example <c>[1,2.5,-3]</c>.</param>
    /// <returns>The vector coordinates.</returns>
    /// <exception cref="FormatException">The text is not valid Db2 vector text.</exception>
    public static float[] Parse(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        ReadOnlySpan<char> span = text.AsSpan().Trim();
        if (span.Length < 2 || span[0] != '[' || span[^1] != ']')
        {
            throw new FormatException("Db2 vector text must be enclosed in square brackets.");
        }

        span = span[1..^1];
        if (span.IsWhiteSpace())
        {
            throw new FormatException("Db2 vector text must contain at least one coordinate.");
        }

        float[] result = new float[span.Count(',') + 1];
        for (int i = 0; i < result.Length; i++)
        {
            int comma = span.IndexOf(',');
            ReadOnlySpan<char> item = comma < 0 ? span : span[..comma];

            if (!float.TryParse(item, NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]))
            {
                throw new FormatException($"Coordinate {i} of the Db2 vector text is not a number: '{item}'.");
            }

            span = comma < 0 ? [] : span[(comma + 1)..];
        }

        return result;
    }
}
