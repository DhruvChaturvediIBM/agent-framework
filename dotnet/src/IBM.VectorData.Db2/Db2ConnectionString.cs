// Copyright (c) IBM Corporation. All rights reserved.

using System.Data.Common;

namespace IBM.VectorData.Db2;

internal static class Db2ConnectionString
{
    /// <summary>Gets the database name, for <see cref="Microsoft.Extensions.VectorData.VectorStoreMetadata"/>.</summary>
    /// <remarks>
    /// The generic parser is used on purpose: the driver's <c>DB2ConnectionStringBuilder</c> constructor throws
    /// <see cref="System.ArgumentNullException"/> when no <c>DB2Connection</c> has been created in the process yet.
    /// </remarks>
    internal static string? GetDatabase(string connectionString)
    {
        DbConnectionStringBuilder builder = new() { ConnectionString = connectionString };

        return builder.TryGetValue("Database", out object? database) || builder.TryGetValue("DB", out database)
            ? database as string
            : null;
    }
}
