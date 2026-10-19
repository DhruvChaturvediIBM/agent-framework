// Copyright (c) IBM Corporation. All rights reserved.

using System;
using System.Data.Common;
using Microsoft.Shared.Diagnostics;

namespace IBM.Data.Db2.Vector;

/// <summary>
/// Extension methods for reading and binding Db2 <c>VECTOR</c> values with the IBM Db2 .NET driver.
/// </summary>
public static class Db2VectorExtensions
{
    /// <summary>
    /// Adds a positional parameter holding a vector. Use it with a <c>?</c> marker produced by
    /// <see cref="Db2VectorSql.VectorFromParameter"/>.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="vector">The vector, or <see langword="null"/> to bind SQL <c>NULL</c>.</param>
    /// <returns>The added parameter.</returns>
    public static DB2Parameter AddVectorParameter(this DB2Command command, ReadOnlyMemory<float>? vector)
    {
        Throw.IfNull(command);

        DB2Parameter parameter = new()
        {
            DB2Type = DB2Type.Clob,
            Value = vector is { } v ? Db2VectorText.Format(v.Span) : DBNull.Value,
        };

        command.Parameters.Add(parameter);
        return parameter;
    }

    /// <summary>Reads a <c>VECTOR</c> column.</summary>
    /// <param name="reader">The data reader.</param>
    /// <param name="ordinal">The zero-based column ordinal.</param>
    /// <returns>The vector coordinates.</returns>
    public static float[] GetVector(this DbDataReader reader, int ordinal)
    {
        Throw.IfNull(reader);

        return Db2VectorText.Parse(reader.GetString(ordinal));
    }
}
