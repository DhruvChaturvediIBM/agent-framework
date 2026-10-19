// Copyright (c) IBM Corporation. All rights reserved.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VectorData.ConformanceTests;

namespace IBM.VectorData.Db2.ConformanceTests;

public class Db2DependencyInjectionTests
    : DependencyInjectionTests<Db2VectorStore, Db2Collection<string, DependencyInjectionTests<string>.Record>, string, DependencyInjectionTests<string>.Record>
{
    // Never opened: these tests only resolve services.
    protected const string ConnectionString = "Server=localhost:50000;Database=TESTDB;UID=user;PWD=password;";

    protected override void PopulateConfiguration(ConfigurationManager configuration, object? serviceKey = null)
        => configuration.AddInMemoryCollection(
        [
            new(CreateConfigKey("Db2", serviceKey, "ConnectionString"), ConnectionString),
        ]);

    private static string ConnectionStringProvider(IServiceProvider sp)
        => sp.GetRequiredService<IConfiguration>().GetRequiredSection("Db2:ConnectionString").Value!;

    private static string ConnectionStringProvider(IServiceProvider sp, object serviceKey)
        => sp.GetRequiredService<IConfiguration>().GetRequiredSection(CreateConfigKey("Db2", serviceKey, "ConnectionString")).Value!;

    public override IEnumerable<Func<IServiceCollection, object?, string, ServiceLifetime, IServiceCollection>> CollectionDelegates
    {
        get
        {
            yield return (services, serviceKey, name, lifetime) => serviceKey is null
                ? services.AddDb2Collection<string, Record>(name, connectionString: ConnectionString, lifetime: lifetime)
                : services.AddKeyedDb2Collection<string, Record>(serviceKey, name, connectionString: ConnectionString, lifetime: lifetime);

            yield return (services, serviceKey, name, lifetime) => serviceKey is null
                ? services.AddDb2Collection<string, Record>(name, ConnectionStringProvider, lifetime: lifetime)
                : services.AddKeyedDb2Collection<string, Record>(serviceKey, name, sp => ConnectionStringProvider(sp, serviceKey), lifetime: lifetime);
        }
    }

    public override IEnumerable<Func<IServiceCollection, object?, ServiceLifetime, IServiceCollection>> StoreDelegates
    {
        get
        {
            yield return (services, serviceKey, lifetime) => serviceKey is null
                ? services.AddDb2VectorStore(ConnectionStringProvider, lifetime: lifetime)
                : services.AddKeyedDb2VectorStore(serviceKey, sp => ConnectionStringProvider(sp, serviceKey), lifetime: lifetime);
        }
    }

    [Fact]
    public void ConnectionStringProviderCantBeNull()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddDb2VectorStore(connectionStringProvider: null!));
        Assert.Throws<ArgumentNullException>(() => services.AddKeyedDb2VectorStore(serviceKey: "notNull", connectionStringProvider: null!));
        Assert.Throws<ArgumentNullException>(() => services.AddDb2Collection<string, Record>(name: "notNull", connectionStringProvider: null!));
        Assert.Throws<ArgumentNullException>(() => services.AddKeyedDb2Collection<string, Record>(serviceKey: "notNull", name: "notNull", connectionStringProvider: null!));
    }

    [Fact]
    public void ConnectionStringCantBeNullOrEmpty()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddDb2Collection<string, Record>(name: "notNull", connectionString: null!));
        Assert.Throws<ArgumentException>(() => services.AddDb2Collection<string, Record>(name: "notNull", connectionString: ""));
        Assert.Throws<ArgumentNullException>(() => services.AddKeyedDb2Collection<string, Record>(serviceKey: "notNull", name: "notNull", connectionString: null!));
        Assert.Throws<ArgumentException>(() => services.AddKeyedDb2Collection<string, Record>(serviceKey: "notNull", name: "notNull", connectionString: ""));
    }
}
