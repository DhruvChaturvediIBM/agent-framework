// Copyright (c) IBM Corporation. All rights reserved.

using System;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Shared.Diagnostics;

namespace IBM.Data.Db2.Vector;

/// <summary>
/// Describes the vector-related capabilities of a Db2 server.
/// </summary>
public sealed class Db2ServerInfo
{
    /// <summary>The first Db2 release with the <c>VECTOR</c> data type and <c>VECTOR_DISTANCE</c>.</summary>
    public static readonly Version MinimumVectorVersion = new(12, 1, 2);

    /// <summary>The first Db2 release with <c>CREATE VECTOR INDEX</c>.</summary>
    public static readonly Version MinimumVectorIndexVersion = new(12, 1, 5);

    private Db2ServerInfo(Version version, bool isTextSearchAvailable)
    {
        this.Version = version;
        this.IsTextSearchAvailable = isTextSearchAvailable;
    }

    /// <summary>Gets the server release, for example 12.1.5.</summary>
    public Version Version { get; }

    /// <summary>Gets a value indicating whether the server supports the <c>VECTOR</c> data type.</summary>
    public bool SupportsVectors => this.Version >= MinimumVectorVersion;

    /// <summary>Gets a value indicating whether the server supports <c>CREATE VECTOR INDEX</c>.</summary>
    public bool SupportsVectorIndex => this.Version >= MinimumVectorIndexVersion;

    /// <summary>Gets a value indicating whether Db2 Text Search (<c>CONTAINS</c> and <c>SCORE</c>) is installed and configured.</summary>
    public bool IsTextSearchAvailable { get; }

    /// <summary>Queries the capabilities of the server behind an open connection.</summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The server capabilities.</returns>
    public static async Task<Db2ServerInfo> GetAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        Throw.IfNull(connection);

        Version version;
        using (DbCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT SERVICE_LEVEL FROM SYSIBMADM.ENV_INST_INFO";
            object? serviceLevel = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            version = ParseServiceLevel(serviceLevel as string);
        }

        bool isTextSearchAvailable;
        using (DbCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT CONTAINS(v, 'probe') FROM (VALUES ('probe')) AS t(v)";
            try
            {
                await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                isTextSearchAvailable = true;
            }
            catch (DbException)
            {
                isTextSearchAvailable = false;
            }
        }

        return new(version, isTextSearchAvailable);
    }

    /// <summary>Parses a Db2 service level such as <c>DB2 v12.1.5.0</c>.</summary>
    internal static Version ParseServiceLevel(string? serviceLevel)
    {
        // The version is the last token; the product name before it ("DB2") contains a digit too.
        ReadOnlySpan<char> span = serviceLevel.AsSpan().Trim();
        span = span[(span.LastIndexOf(' ') + 1)..].TrimStart("vV");

        if (Version.TryParse(span, out Version? version))
        {
            return version;
        }

        throw new FormatException(string.Create(CultureInfo.InvariantCulture, $"Unrecognized Db2 service level '{serviceLevel}'."));
    }
}
