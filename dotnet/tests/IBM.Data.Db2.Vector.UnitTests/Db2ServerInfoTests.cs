// Copyright (c) IBM Corporation. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace IBM.Data.Db2.Vector.UnitTests;

public class Db2ServerInfoTests
{
    [Theory]
    [InlineData("DB2 v12.1.5.0", "12.1.5.0")]
    [InlineData("DB2 v12.1.2.0 ", "12.1.2.0")]
    [InlineData("DB2 v11.5.9.0", "11.5.9.0")]
    public void ParseServiceLevel_ReadsVersion(string serviceLevel, string expected)
    {
        Assert.Equal(Version.Parse(expected), Db2ServerInfo.ParseServiceLevel(serviceLevel));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("DB2")]
    public void ParseServiceLevel_RejectsUnknownFormat(string? serviceLevel)
    {
        Assert.Throws<FormatException>(() => Db2ServerInfo.ParseServiceLevel(serviceLevel));
    }

    [Fact]
    public void MinimumVersions_MatchDb2Releases()
    {
        Assert.Equal(new Version(12, 1, 2), Db2ServerInfo.MinimumVectorVersion);
        Assert.Equal(new Version(12, 1, 5), Db2ServerInfo.MinimumVectorIndexVersion);
    }

    [Fact]
    public async Task GetAsync_ReadsVersionAndTextSearchAsync()
    {
        using FakeConnection connection = new(serviceLevel: "DB2 v12.1.5.0", textSearchInstalled: true);

        var info = await Db2ServerInfo.GetAsync(connection);

        Assert.Equal(new Version(12, 1, 5, 0), info.Version);
        Assert.True(info.SupportsVectors);
        Assert.True(info.SupportsVectorIndex);
        Assert.True(info.IsTextSearchAvailable);
        Assert.Equal(["SELECT SERVICE_LEVEL FROM SYSIBMADM.ENV_INST_INFO", "SELECT CONTAINS(v, 'probe') FROM (VALUES ('probe')) AS t(v)"], connection.Commands);
    }

    [Theory]
    [InlineData("DB2 v12.1.2.0", true, false)]
    [InlineData("DB2 v11.5.9.0", false, false)]
    public async Task GetAsync_ReportsVersionDependentFeaturesAsync(string serviceLevel, bool vectors, bool vectorIndex)
    {
        using FakeConnection connection = new(serviceLevel, textSearchInstalled: false);

        var info = await Db2ServerInfo.GetAsync(connection);

        Assert.Equal(vectors, info.SupportsVectors);
        Assert.Equal(vectorIndex, info.SupportsVectorIndex);
        Assert.False(info.IsTextSearchAvailable);
    }

    [Fact]
    public async Task GetAsync_ValidatesConnectionAsync()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Db2ServerInfo.GetAsync(null!));
    }

    private sealed class FakeConnection(string serviceLevel, bool textSearchInstalled) : DbConnection
    {
        public List<string> Commands { get; } = [];

        [AllowNull]
        public override string ConnectionString { get; set; } = "";

        public override string Database => "TESTDB";

        public override string DataSource => "fake";

        public override string ServerVersion => "12.01.0500";

        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public override void Close()
        {
        }

        public override void Open()
        {
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => new FakeCommand(this);

        internal object? Execute(string sql)
        {
            this.Commands.Add(sql);
            return sql.Contains("SERVICE_LEVEL", StringComparison.Ordinal) ? serviceLevel
                : textSearchInstalled ? 1
                : throw new FakeDbException();
        }
    }

    private sealed class FakeCommand(FakeConnection connection) : DbCommand
    {
        [AllowNull]
        public override string CommandText { get; set; } = "";

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection? DbConnection { get; set; }

        protected override DbParameterCollection DbParameterCollection => throw new NotSupportedException();

        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery() => throw new NotSupportedException();

        public override object? ExecuteScalar() => connection.Execute(this.CommandText);

        public override void Prepare()
        {
        }

        protected override DbParameter CreateDbParameter() => throw new NotSupportedException();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
    }

    private sealed class FakeDbException : DbException;
}
