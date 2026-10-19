// Copyright (c) IBM Corporation. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;

namespace IBM.VectorData.Db2;

internal sealed class Db2ModelBuilder() : CollectionModelBuilder(s_modelBuildingOptions)
{
    internal const string SupportedVectorTypes = "ReadOnlyMemory<float>, Embedding<float>, float[]";

    private static readonly CollectionModelBuildingOptions s_modelBuildingOptions = new()
    {
        RequiresAtLeastOneVector = false,
        SupportsMultipleVectors = true,
    };

    protected override void ValidateKeyProperty(KeyPropertyModel keyProperty)
    {
        base.ValidateKeyProperty(keyProperty);

        var type = keyProperty.Type;

        if (type != typeof(int) && type != typeof(long) && type != typeof(string) && type != typeof(Guid))
        {
            throw new NotSupportedException(
                $"Property '{keyProperty.ModelName}' has unsupported type '{type.Name}'. Key properties must be one of the supported types: int, long, string, Guid.");
        }
    }

    protected override void ValidateProperty(PropertyModel propertyModel, VectorStoreCollectionDefinition? definition)
    {
        base.ValidateProperty(propertyModel, definition);

        if (propertyModel is VectorPropertyModel vectorProperty)
        {
            if (vectorProperty.IndexKind is not (IndexKind.Flat or null or ""))
            {
                throw new NotSupportedException(
                    $"Index kind '{vectorProperty.IndexKind}' is not supported by the Db2 provider. Supported index kinds: {IndexKind.Flat}");
            }

            _ = Db2DistanceFunctions.Get(vectorProperty.DistanceFunction);
        }
    }

    protected override bool IsDataPropertyTypeValid(Type type, [NotNullWhen(false)] out string? supportedTypes)
    {
        supportedTypes = "string, short, int, long, double, float, decimal, bool, DateTime, DateOnly, TimeOnly, Guid, byte[], string[], List<string>";

        if (Nullable.GetUnderlyingType(type) is Type underlyingType)
        {
            type = underlyingType;
        }

        return type == typeof(short) // SMALLINT
            || type == typeof(int) // INTEGER
            || type == typeof(long) // BIGINT
            || type == typeof(string) // VARCHAR
            || type == typeof(Guid) // CHAR(36)
            || type == typeof(byte[]) // VARBINARY
            || type == typeof(bool) // BOOLEAN
            || type == typeof(DateTime) // TIMESTAMP(7)
            || type == typeof(DateOnly) // DATE
            || type == typeof(TimeOnly) // TIME
            || type == typeof(decimal) // DECIMAL
            || type == typeof(double) // DOUBLE
            || type == typeof(float) // REAL
            || type == typeof(string[]) // VARCHAR holding a JSON array
            || type == typeof(List<string>); // VARCHAR holding a JSON array
    }

    protected override bool IsVectorPropertyTypeValid(Type type, [NotNullWhen(false)] out string? supportedTypes)
        => IsVectorPropertyTypeValidCore(type, out supportedTypes);

    internal static bool IsVectorPropertyTypeValidCore(Type type, [NotNullWhen(false)] out string? supportedTypes)
    {
        supportedTypes = SupportedVectorTypes;

        return type == typeof(ReadOnlyMemory<float>)
            || type == typeof(ReadOnlyMemory<float>?)
            || type == typeof(Embedding<float>)
            || type == typeof(float[]);
    }
}
