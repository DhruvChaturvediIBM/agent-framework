// Copyright (c) IBM Corporation. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using IBM.Data.Db2;
using IBM.Data.Db2.Vector;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;
using Microsoft.Shared.Diagnostics;

#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities: identifiers are quoted, values are parameterized.

namespace IBM.VectorData.Db2;

/// <summary>
/// Builds the SQL commands used by <see cref="Db2Collection{TKey, TRecord}"/>.
/// </summary>
/// <remarks>
/// The Db2 .NET driver binds parameters by position, so every <c>?</c> marker must be appended to the SQL
/// in the same order as its value is added to the command.
/// </remarks>
internal static class Db2CommandBuilder
{
    // Column aliases used by vector search; the leading underscore avoids clashes with user columns.
    internal const string DistanceColumn = "_db2_distance";
    internal const string ScoreColumn = "_db2_score";

    internal static List<DB2Command> CreateTable(DB2Connection connection, string? schema, string tableName, CollectionModel model)
    {
        StringBuilder sb = new(200);
        sb.Append("CREATE TABLE ").AppendTableName(schema, tableName).AppendLine(" (");

        foreach (var property in model.Properties)
        {
            sb.AppendIdentifier(property.StorageName).Append(' ').Append(Map(property));
            if (property is KeyPropertyModel || !property.IsNullable)
            {
                sb.Append(" NOT NULL");
            }

            sb.AppendLine(",");
        }

        sb.Append("PRIMARY KEY (").AppendIdentifier(model.KeyProperty.StorageName).Append("))");

        List<DB2Command> commands = [connection.CreateCommand(sb)];

        foreach (var dataProperty in model.DataProperties)
        {
            // A B-tree index over the JSON text of an array cannot serve element lookups, and would exceed the index key length.
            if (dataProperty.IsIndexed && dataProperty.Type != typeof(string[]) && dataProperty.Type != typeof(List<string>))
            {
                sb.Clear();
                sb.Append("CREATE INDEX ");
                sb.AppendIndexName(schema, tableName, dataProperty.StorageName);
                sb.Append(" ON ").AppendTableName(schema, tableName);
                sb.Append(" (").AppendIdentifier(dataProperty.StorageName).Append(')');
                commands.Add(connection.CreateCommand(sb));
            }
        }

        return commands;
    }

    internal static DB2Command DropTableIfExists(DB2Connection connection, string? schema, string tableName)
    {
        StringBuilder sb = new(50);
        sb.Append("DROP TABLE IF EXISTS ").AppendTableName(schema, tableName);

        return connection.CreateCommand(sb);
    }

    internal static DB2Command SelectTableName(DB2Connection connection, string? schema, string tableName)
    {
        DB2Command command = SelectTableNames(connection, schema);
        command.CommandText += " AND TABNAME = ?";
        command.Parameters.Add(new DB2Parameter { DB2Type = DB2Type.VarChar, Value = tableName });
        return command;
    }

    internal static DB2Command SelectTableNames(DB2Connection connection, string? schema)
    {
        DB2Command command = connection.CreateCommand();
        command.CommandText = "SELECT TABNAME FROM SYSCAT.TABLES WHERE TYPE = 'T' AND TABSCHEMA = COALESCE(CAST(? AS VARCHAR(128)), CURRENT SCHEMA)";
        command.Parameters.Add(new DB2Parameter { DB2Type = DB2Type.VarChar, Value = string.IsNullOrEmpty(schema) ? DBNull.Value : schema });
        return command;
    }

    /// <summary>Builds a single-row <c>MERGE</c> that inserts the record or replaces all of its columns.</summary>
    internal static void Upsert(
        DB2Command command,
        string? schema,
        string tableName,
        CollectionModel model,
        object record,
        Func<VectorPropertyModel, object?>? getGeneratedEmbedding)
    {
        StringBuilder sb = new(500);
        sb.Append("MERGE INTO ").AppendTableName(schema, tableName).AppendLine(" AS t");
        sb.Append("USING (VALUES (");

        foreach (var property in model.Properties)
        {
            if (property is VectorPropertyModel vectorProperty)
            {
                sb.Append(Db2VectorSql.VectorFromParameter(vectorProperty.Dimensions)).Append(',');

                var value = getGeneratedEmbedding?.Invoke(vectorProperty) ?? vectorProperty.GetValueAsObject(record);
                command.AddParameter(vectorProperty, value);
            }
            else
            {
                sb.Append("CAST(? AS ").Append(Map(property)).Append("),");
                command.AddParameter(property, property.GetValueAsObject(record));
            }
        }

        sb[sb.Length - 1] = ')'; // replace the last comma
        sb.Append(") AS s (").AppendIdentifiers(model.Properties).AppendLine(")");
        sb.Append("ON t.").AppendIdentifier(model.KeyProperty.StorageName).Append(" = s.").AppendIdentifier(model.KeyProperty.StorageName).AppendLine();

        if (model.Properties.Count > 1)
        {
            sb.Append("WHEN MATCHED THEN UPDATE SET ");
            foreach (var property in model.Properties)
            {
                if (property is not KeyPropertyModel)
                {
                    sb.AppendIdentifier(property.StorageName).Append(" = s.").AppendIdentifier(property.StorageName).Append(',');
                }
            }

            --sb.Length; // remove the last comma
            sb.AppendLine();
        }

        sb.Append("WHEN NOT MATCHED THEN INSERT (").AppendIdentifiers(model.Properties).Append(") VALUES (");
        sb.AppendIdentifiers(model.Properties, prefix: "s.").Append(')');

        command.CommandText = sb.ToString();
    }

    internal static DB2Command DeleteSingle(DB2Connection connection, string? schema, string tableName, KeyPropertyModel keyProperty, object key)
    {
        DB2Command command = connection.CreateCommand();

        StringBuilder sb = new(100);
        sb.Append("DELETE FROM ").AppendTableName(schema, tableName);
        sb.Append(" WHERE ").AppendIdentifier(keyProperty.StorageName).Append(" = ?");
        command.AddParameter(keyProperty, key);

        command.CommandText = sb.ToString();
        return command;
    }

    internal static bool DeleteMany<TKey>(DB2Command command, string? schema, string tableName, KeyPropertyModel keyProperty, IEnumerable<TKey> keys)
    {
        StringBuilder sb = new(100);
        sb.Append("DELETE FROM ").AppendTableName(schema, tableName);
        sb.Append(" WHERE ").AppendIdentifier(keyProperty.StorageName).Append(" IN (");

        if (!sb.AppendKeyParameterList(keys, command, keyProperty))
        {
            return false;
        }

        sb.Append(')');
        command.CommandText = sb.ToString();
        return true;
    }

    internal static DB2Command SelectSingle(DB2Connection connection, string? schema, string tableName, CollectionModel model, object key, bool includeVectors)
    {
        DB2Command command = connection.CreateCommand();

        StringBuilder sb = new(200);
        sb.Append("SELECT ").AppendIdentifiers(model.Properties, includeVectors: includeVectors);
        sb.Append(" FROM ").AppendTableName(schema, tableName);
        sb.Append(" WHERE ").AppendIdentifier(model.KeyProperty.StorageName).Append(" = ?");
        command.AddParameter(model.KeyProperty, key);

        command.CommandText = sb.ToString();
        return command;
    }

    internal static bool SelectMany<TKey>(DB2Command command, string? schema, string tableName, CollectionModel model, IEnumerable<TKey> keys, bool includeVectors)
    {
        StringBuilder sb = new(200);
        sb.Append("SELECT ").AppendIdentifiers(model.Properties, includeVectors: includeVectors);
        sb.Append(" FROM ").AppendTableName(schema, tableName);
        sb.Append(" WHERE ").AppendIdentifier(model.KeyProperty.StorageName).Append(" IN (");

        if (!sb.AppendKeyParameterList(keys, command, model.KeyProperty))
        {
            return false;
        }

        sb.Append(')');
        command.CommandText = sb.ToString();
        return true;
    }

    internal static DB2Command SelectVector<TRecord>(
        DB2Connection connection,
        string? schema,
        string tableName,
        VectorPropertyModel vectorProperty,
        CollectionModel model,
        int top,
        VectorSearchOptions<TRecord> options,
        ReadOnlyMemory<float> vector)
    {
        var distanceFunction = Db2DistanceFunctions.Get(vectorProperty.DistanceFunction);
        DB2Command command = connection.CreateCommand();

        StringBuilder sb = new(400);

        // The distance is computed once in an inner query, so the outer query can filter, order and score on it.
        sb.Append("SELECT ").AppendIdentifiers(model.Properties, includeVectors: options.IncludeVectors).Append(", ");
        sb.Append(distanceFunction.ScoreOffset).Append(" + (").Append(distanceFunction.ScoreSign).Append(" * ").AppendIdentifier(DistanceColumn).Append(") AS ").AppendIdentifier(ScoreColumn);
        sb.AppendLine(" FROM (");

        sb.Append("SELECT ").AppendIdentifiers(model.Properties, includeVectors: options.IncludeVectors).Append(", ");
        sb.Append(Db2VectorSql.Distance(
            QuoteIdentifier(vectorProperty.StorageName),
            Db2VectorSql.VectorFromParameter(vector.Length),
            distanceFunction.Metric));
        sb.Append(" AS ").AppendIdentifier(DistanceColumn);
        command.AddParameter(vectorProperty, vector);

        sb.Append(" FROM ").AppendTableName(schema, tableName);

        // Records without a vector have no distance and can never be a search result.
        sb.Append(" WHERE ").AppendIdentifier(vectorProperty.StorageName).Append(" IS NOT NULL");

        if (options.Filter is not null)
        {
            sb.Append(" AND ");
            command.AppendFilter(sb, model, options.Filter);
        }

        sb.AppendLine(") AS i");

        if (options.ScoreThreshold is { } scoreThreshold)
        {
            // Translate the threshold on the score into a threshold on the distance:
            // score = offset + sign * distance, and sign is -1 for similarities and 1 for distances.
            sb.Append("WHERE ").AppendIdentifier(DistanceColumn).AppendLine(" <= ?");
            command.Parameters.Add(new DB2Parameter
            {
                DB2Type = DB2Type.Double,
                Value = distanceFunction.IsSimilarity ? distanceFunction.ScoreOffset - scoreThreshold : scoreThreshold,
            });
        }

        sb.Append("ORDER BY ").AppendIdentifier(DistanceColumn).AppendLine(" ASC");
        sb.Append("OFFSET ").Append(options.Skip).Append(" ROWS FETCH NEXT ").Append(top).Append(" ROWS ONLY");

        command.CommandText = sb.ToString();
        return command;
    }

    internal static DB2Command SelectWhere<TRecord>(
        Expression<Func<TRecord, bool>> filter,
        int top,
        FilteredRecordRetrievalOptions<TRecord> options,
        DB2Connection connection,
        string? schema,
        string tableName,
        CollectionModel model)
    {
        DB2Command command = connection.CreateCommand();

        StringBuilder sb = new(200);
        sb.Append("SELECT ").AppendIdentifiers(model.Properties, includeVectors: options.IncludeVectors);
        sb.Append(" FROM ").AppendTableName(schema, tableName);
        sb.Append(" WHERE ");
        command.AppendFilter(sb, model, filter);
        sb.AppendLine();

        var orderBy = options.OrderBy?.Invoke(new()).Values;
        if (orderBy is { Count: > 0 })
        {
            sb.Append("ORDER BY ");
            foreach (var sortInfo in orderBy)
            {
                sb.AppendIdentifier(model.GetDataOrKeyProperty(sortInfo.PropertySelector).StorageName)
                    .Append(sortInfo.Ascending ? " ASC," : " DESC,");
            }

            --sb.Length; // remove the last comma
            sb.AppendLine();
        }

        sb.Append("OFFSET ").Append(options.Skip).Append(" ROWS FETCH NEXT ").Append(top).Append(" ROWS ONLY");

        command.CommandText = sb.ToString();
        return command;
    }

    internal static string QuoteIdentifier(string identifier)
        => new StringBuilder(identifier.Length + 2).AppendIdentifier(identifier).ToString();

    /// <summary>Appends a delimited Db2 identifier; embedded double quotes are doubled.</summary>
    internal static StringBuilder AppendIdentifier(this StringBuilder sb, string identifier)
    {
        sb.Append('"');
        int start = sb.Length;
        sb.Append(identifier);
        sb.Replace("\"", "\"\"", start, identifier.Length);
        return sb.Append('"');
    }

    internal static StringBuilder AppendTableName(this StringBuilder sb, string? schema, string tableName)
    {
        if (!string.IsNullOrEmpty(schema))
        {
            sb.AppendIdentifier(schema).Append('.');
        }

        return sb.AppendIdentifier(tableName);
    }

    /// <summary>Binds a value for a property, converting it to what the Db2 driver accepts for the column type.</summary>
    internal static void AddParameter(this DB2Command command, PropertyModel? property, object? value)
    {
        switch (value)
        {
            case null when property is VectorPropertyModel:
                command.AddVectorParameter(null);
                break;
            case null:
                command.Parameters.Add(new DB2Parameter { Value = DBNull.Value });
                break;

            case ReadOnlyMemory<float> vector:
                command.AddVectorParameter(vector);
                break;
            case Embedding<float> embedding:
                command.AddVectorParameter(embedding.Vector);
                break;
            case float[] vector:
                command.AddVectorParameter(vector);
                break;

            case Guid guid:
                command.Parameters.Add(new DB2Parameter { DB2Type = DB2Type.Char, Value = guid.ToString("D") });
                break;
            case DateTime dateTime:
                command.Parameters.Add(new DB2Parameter { DB2Type = DB2Type.Timestamp, Value = dateTime });
                break;
            case DateOnly date:
                command.Parameters.Add(new DB2Parameter { DB2Type = DB2Type.Date, Value = date.ToDateTime(TimeOnly.MinValue) });
                break;
            case TimeOnly time:
                command.Parameters.Add(new DB2Parameter { DB2Type = DB2Type.Time, Value = time.ToTimeSpan() });
                break;
            case byte[] bytes:
                command.Parameters.Add(new DB2Parameter { DB2Type = DB2Type.VarBinary, Value = bytes });
                break;
            case string[] strings:
                command.Parameters.Add(new DB2Parameter { DB2Type = DB2Type.VarChar, Value = Db2Json.Serialize(strings) });
                break;
            case List<string> strings:
                command.Parameters.Add(new DB2Parameter { DB2Type = DB2Type.VarChar, Value = Db2Json.Serialize(strings) });
                break;

            default:
                command.Parameters.Add(new DB2Parameter { Value = value });
                break;
        }
    }

    private static void AppendFilter<TRecord>(this DB2Command command, StringBuilder sb, CollectionModel model, Expression<Func<TRecord, bool>> filter)
    {
        Db2FilterTranslator translator = new(model, filter, sb);
        translator.Translate();

        foreach (object? parameter in translator.ParameterValues)
        {
            command.AddParameter(property: null, parameter);
        }
    }

    private static StringBuilder AppendIdentifiers(
        this StringBuilder sb,
        IEnumerable<PropertyModel> properties,
        string? prefix = null,
        bool includeVectors = true)
    {
        bool any = false;
        foreach (var property in properties)
        {
            if (!includeVectors && property is VectorPropertyModel)
            {
                continue;
            }

            sb.Append(prefix).AppendIdentifier(property.StorageName).Append(',');
            any = true;
        }

        if (any)
        {
            --sb.Length; // remove the last comma
        }

        return sb;
    }

    private static bool AppendKeyParameterList<TKey>(this StringBuilder sb, IEnumerable<TKey> keys, DB2Command command, KeyPropertyModel keyProperty)
    {
        bool any = false;
        foreach (TKey key in keys)
        {
            Throw.IfNull(key);

            sb.Append("?,");
            command.AddParameter(keyProperty, key);
            any = true;
        }

        if (any)
        {
            --sb.Length; // remove the last comma
        }

        return any;
    }

    private static StringBuilder AppendIndexName(this StringBuilder sb, string? schema, string tableName, string columnName)
    {
        // Index names are limited to 128 bytes; keep letters, digits and underscores only.
        StringBuilder name = new("IDX_");
        AppendAllowedOnly(name, tableName);
        name.Append('_');
        AppendAllowedOnly(name, columnName);

        if (name.Length > 128)
        {
            name.Length = 128;
        }

        if (!string.IsNullOrEmpty(schema))
        {
            sb.AppendIdentifier(schema).Append('.');
        }

        return sb.AppendIdentifier(name.ToString());

        static void AppendAllowedOnly(StringBuilder sb, string value)
        {
            foreach (char c in value)
            {
                if (char.IsAsciiLetterOrDigit(c) || c == '_')
                {
                    sb.Append(c);
                }
            }
        }
    }

    private static DB2Command CreateCommand(this DB2Connection connection, StringBuilder sb)
    {
        DB2Command command = connection.CreateCommand();
        command.CommandText = sb.ToString();
        return command;
    }

    internal static string Map(PropertyModel property)
        => property is VectorPropertyModel vectorProperty
            // Checked first: with embedding generation the vector property's CLR type is the input type, e.g. string.
            ? Db2VectorSql.ColumnType(vectorProperty.Dimensions)
            : MapDataType(property);

    private static string MapDataType(PropertyModel property)
        => (Nullable.GetUnderlyingType(property.Type) ?? property.Type) switch
        {
            Type t when t == typeof(short) => "SMALLINT",
            Type t when t == typeof(int) => "INTEGER",
            Type t when t == typeof(long) => "BIGINT",
            Type t when t == typeof(Guid) => "CHAR(36)",
            Type t when t == typeof(string) && property is KeyPropertyModel or DataPropertyModel { IsIndexed: true }
                => string.Create(CultureInfo.InvariantCulture, $"VARCHAR({Db2Constants.MaxIndexedStringLength})"),
            Type t when t == typeof(string) || t == typeof(string[]) || t == typeof(List<string>)
                => string.Create(CultureInfo.InvariantCulture, $"VARCHAR({Db2Constants.MaxStringLength})"),
            Type t when t == typeof(byte[]) => string.Create(CultureInfo.InvariantCulture, $"VARBINARY({Db2Constants.MaxStringLength})"),
            Type t when t == typeof(bool) => "BOOLEAN",
            Type t when t == typeof(DateTime) => "TIMESTAMP(7)",
            Type t when t == typeof(DateOnly) => "DATE",
            Type t when t == typeof(TimeOnly) => "TIME",
            Type t when t == typeof(decimal) => "DECIMAL(31,8)",
            Type t when t == typeof(double) => "DOUBLE",
            Type t when t == typeof(float) => "REAL",

            _ => throw new NotSupportedException($"Type {property.Type} is not supported."),
        };
}
