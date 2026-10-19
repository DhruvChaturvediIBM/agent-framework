// Copyright (c) IBM Corporation. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using IBM.Data.Db2.Vector;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData.ProviderServices;

namespace IBM.VectorData.Db2;

internal sealed class Db2Mapper<TRecord>(CollectionModel model)
{
    public TRecord MapFromStorageToDataModel(DbDataReader reader, bool includeVectors)
    {
        var record = model.CreateRecord<TRecord>()!;

        PopulateValue(reader, model.KeyProperty, record);

        foreach (var property in model.DataProperties)
        {
            PopulateValue(reader, property, record);
        }

        if (includeVectors)
        {
            foreach (var property in model.VectorProperties)
            {
                try
                {
                    var ordinal = reader.GetOrdinal(property.StorageName);

                    if (!reader.IsDBNull(ordinal))
                    {
                        float[] vector = reader.GetVector(ordinal);

                        property.SetValueAsObject(record, (Nullable.GetUnderlyingType(property.Type) ?? property.Type) switch
                        {
                            var t when t == typeof(ReadOnlyMemory<float>) => new ReadOnlyMemory<float>(vector),
                            var t when t == typeof(Embedding<float>) => new Embedding<float>(vector),
                            var t when t == typeof(float[]) => vector,

                            _ => throw new UnreachableException(),
                        });
                    }
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException($"Failed to deserialize vector property '{property.ModelName}'.", e);
                }
            }
        }

        return record;

        static void PopulateValue(DbDataReader reader, PropertyModel property, object record)
        {
            try
            {
                var ordinal = reader.GetOrdinal(property.StorageName);

                if (reader.IsDBNull(ordinal))
                {
                    property.SetValueAsObject(record, null);
                    return;
                }

                switch (Nullable.GetUnderlyingType(property.Type) ?? property.Type)
                {
                    case var t when t == typeof(short):
                        property.SetValue(record, reader.GetInt16(ordinal)); // SMALLINT
                        break;
                    case var t when t == typeof(int):
                        property.SetValue(record, reader.GetInt32(ordinal)); // INTEGER
                        break;
                    case var t when t == typeof(long):
                        property.SetValue(record, reader.GetInt64(ordinal)); // BIGINT
                        break;

                    case var t when t == typeof(float):
                        property.SetValue(record, reader.GetFloat(ordinal)); // REAL
                        break;
                    case var t when t == typeof(double):
                        property.SetValue(record, reader.GetDouble(ordinal)); // DOUBLE
                        break;
                    case var t when t == typeof(decimal):
                        property.SetValue(record, reader.GetDecimal(ordinal)); // DECIMAL
                        break;

                    case var t when t == typeof(string):
                        property.SetValue(record, reader.GetString(ordinal)); // VARCHAR
                        break;
                    case var t when t == typeof(Guid):
                        property.SetValue(record, Guid.Parse(reader.GetString(ordinal))); // CHAR(36)
                        break;
                    case var t when t == typeof(byte[]):
                        property.SetValueAsObject(record, reader.GetValue(ordinal)); // VARBINARY
                        break;
                    case var t when t == typeof(bool):
                        property.SetValue(record, reader.GetBoolean(ordinal)); // BOOLEAN
                        break;

                    case var t when t == typeof(DateTime):
                        property.SetValue(record, reader.GetDateTime(ordinal)); // TIMESTAMP
                        break;
                    case var t when t == typeof(DateOnly):
                        property.SetValue(record, DateOnly.FromDateTime(reader.GetDateTime(ordinal))); // DATE
                        break;
                    case var t when t == typeof(TimeOnly):
                        property.SetValue(record, TimeOnly.FromTimeSpan((TimeSpan)reader.GetValue(ordinal))); // TIME
                        break;

                    case var t when t == typeof(string[]):
                        property.SetValue(record, Db2Json.DeserializeStringArray(reader.GetString(ordinal))); // VARCHAR (JSON)
                        break;
                    case var t when t == typeof(List<string>):
                        property.SetValue(record, Db2Json.DeserializeStringList(reader.GetString(ordinal))); // VARCHAR (JSON)
                        break;

                    default:
                        throw new NotSupportedException($"Unsupported type '{property.Type.Name}' for property '{property.ModelName}'.");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to read property '{property.ModelName}' of type '{property.Type.Name}'.", ex);
            }
        }
    }
}
