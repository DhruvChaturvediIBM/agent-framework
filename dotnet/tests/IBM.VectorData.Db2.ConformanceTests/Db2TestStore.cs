// Copyright (c) IBM Corporation. All rights reserved.

using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.VectorData;
using VectorData.ConformanceTests.Support;

namespace IBM.VectorData.Db2.ConformanceTests;

/// <summary>
/// Runs the conformance suite against a live Db2 12.1.2+ database. Configure it with the environment variable
/// or user secret <c>Db2:ConnectionString</c>, or with <c>DB2_HOST</c>, <c>DB2_PORT</c>, <c>DB2_NAME</c>,
/// <c>DB2_USERNAME</c>, <c>DB2_PASSWORD</c> and optionally <c>DB2_SSL=true</c>.
/// </summary>
internal sealed class Db2TestStore : TestStore
{
    private static readonly string? s_connectionString = ReadConnectionString();

    public static Db2TestStore Instance { get; } = new();

    private Db2TestStore()
    {
    }

    public string ConnectionString => s_connectionString
        ?? throw new InvalidOperationException("No Db2 connection configured: set Db2:ConnectionString or the DB2_* environment variables.");

    public override string DefaultDistanceFunction => DistanceFunction.CosineDistance;

    public Db2VectorStore GetVectorStore(Db2VectorStoreOptions options)
        => new(this.ConnectionString, options);

    protected override Task StartAsync()
    {
        this.DefaultVectorStore = new Db2VectorStore(this.ConnectionString);
        return Task.CompletedTask;
    }

    private static string? ReadConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddUserSecrets<Db2TestStore>()
            .Build();

        if (configuration["Db2:ConnectionString"] is { Length: > 0 } connectionString)
        {
            return connectionString;
        }

        if (configuration["DB2_HOST"] is not { Length: > 0 } host)
        {
            return null;
        }

        // Not DB2ConnectionStringBuilder: its constructor fails before the first DB2Connection is created.
        DbConnectionStringBuilder builder = new()
        {
            ["Server"] = $"{host}:{configuration["DB2_PORT"] ?? "50000"}",
            ["Database"] = configuration["DB2_NAME"],
            ["UID"] = configuration["DB2_USERNAME"],
            ["PWD"] = configuration["DB2_PASSWORD"],
        };

        if (string.Equals(configuration["DB2_SSL"], "true", StringComparison.OrdinalIgnoreCase))
        {
            builder["Security"] = "SSL";
        }

        return builder.ConnectionString;
    }
}
