// Copyright (c) IBM Corporation. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;
using IBM.VectorData.Db2;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.Shared.Diagnostics;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods to register <see cref="Db2VectorStore"/> and <see cref="Db2Collection{TKey, TRecord}"/> instances on an <see cref="IServiceCollection"/>.
/// </summary>
public static class Db2ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="Db2VectorStore"/> as <see cref="VectorStore"/>, with the specified connection string and service lifetime.
    /// </summary>
    /// <inheritdoc cref="AddKeyedDb2VectorStore(IServiceCollection, object?, Func{IServiceProvider, string}, Func{IServiceProvider, Db2VectorStoreOptions}?, ServiceLifetime)"/>
    [RequiresUnreferencedCode("The Db2 provider is currently incompatible with trimming.")]
    [RequiresDynamicCode("The Db2 provider is currently incompatible with NativeAOT.")]
    public static IServiceCollection AddDb2VectorStore(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionStringProvider,
        Func<IServiceProvider, Db2VectorStoreOptions>? optionsProvider = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        => AddKeyedDb2VectorStore(services, serviceKey: null, connectionStringProvider, optionsProvider, lifetime);

    /// <summary>
    /// Registers a keyed <see cref="Db2VectorStore"/> as <see cref="VectorStore"/>, with the specified connection string and service lifetime.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="VectorStore"/> on.</param>
    /// <param name="serviceKey">The key with which to associate the vector store.</param>
    /// <param name="connectionStringProvider">The connection string provider.</param>
    /// <param name="optionsProvider">Options provider to further configure the vector store.</param>
    /// <param name="lifetime">The service lifetime for the store. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>The service collection.</returns>
    [RequiresUnreferencedCode("The Db2 provider is currently incompatible with trimming.")]
    [RequiresDynamicCode("The Db2 provider is currently incompatible with NativeAOT.")]
    public static IServiceCollection AddKeyedDb2VectorStore(
        this IServiceCollection services,
        object? serviceKey,
        Func<IServiceProvider, string> connectionStringProvider,
        Func<IServiceProvider, Db2VectorStoreOptions>? optionsProvider = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        Throw.IfNull(services);
        Throw.IfNull(connectionStringProvider);

        services.Add(new ServiceDescriptor(typeof(Db2VectorStore), serviceKey, (sp, _) =>
        {
            var connectionString = connectionStringProvider(sp);
            var options = GetStoreOptions(sp, optionsProvider);
            return new Db2VectorStore(connectionString, options);
        }, lifetime));

        services.Add(new ServiceDescriptor(typeof(VectorStore), serviceKey,
            static (sp, key) => sp.GetRequiredKeyedService<Db2VectorStore>(key), lifetime));

        return services;
    }

    /// <summary>
    /// Registers a <see cref="Db2Collection{TKey, TRecord}"/> as <see cref="VectorStoreCollection{TKey, TRecord}"/>, with the specified connection string and service lifetime.
    /// </summary>
    /// <inheritdoc cref="AddKeyedDb2Collection{TKey, TRecord}(IServiceCollection, object?, string, Func{IServiceProvider, string}, Func{IServiceProvider, Db2CollectionOptions}?, ServiceLifetime)"/>
    [RequiresUnreferencedCode("The Db2 provider is currently incompatible with trimming.")]
    [RequiresDynamicCode("The Db2 provider is currently incompatible with NativeAOT.")]
    public static IServiceCollection AddDb2Collection<TKey, TRecord>(
        this IServiceCollection services,
        string name,
        Func<IServiceProvider, string> connectionStringProvider,
        Func<IServiceProvider, Db2CollectionOptions>? optionsProvider = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TKey : notnull
        where TRecord : class
        => AddKeyedDb2Collection<TKey, TRecord>(services, serviceKey: null, name, connectionStringProvider, optionsProvider, lifetime);

    /// <summary>
    /// Registers a keyed <see cref="Db2Collection{TKey, TRecord}"/> as <see cref="VectorStoreCollection{TKey, TRecord}"/>, with the specified connection string and service lifetime.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="VectorStoreCollection{TKey, TRecord}"/> on.</param>
    /// <param name="serviceKey">The key with which to associate the collection.</param>
    /// <param name="name">The name of the collection.</param>
    /// <param name="connectionStringProvider">The connection string provider.</param>
    /// <param name="optionsProvider">Options provider to further configure the collection.</param>
    /// <param name="lifetime">The service lifetime for the store. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>The service collection.</returns>
    [RequiresUnreferencedCode("The Db2 provider is currently incompatible with trimming.")]
    [RequiresDynamicCode("The Db2 provider is currently incompatible with NativeAOT.")]
    public static IServiceCollection AddKeyedDb2Collection<TKey, TRecord>(
        this IServiceCollection services,
        object? serviceKey,
        string name,
        Func<IServiceProvider, string> connectionStringProvider,
        Func<IServiceProvider, Db2CollectionOptions>? optionsProvider = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TKey : notnull
        where TRecord : class
    {
        Throw.IfNull(services);
        Throw.IfNullOrWhitespace(name);
        Throw.IfNull(connectionStringProvider);

        services.Add(new ServiceDescriptor(typeof(Db2Collection<TKey, TRecord>), serviceKey, (sp, _) =>
        {
            var connectionString = connectionStringProvider(sp);
            var options = GetCollectionOptions(sp, optionsProvider);
            return new Db2Collection<TKey, TRecord>(connectionString, name, options);
        }, lifetime));

        services.Add(new ServiceDescriptor(typeof(VectorStoreCollection<TKey, TRecord>), serviceKey,
            static (sp, key) => sp.GetRequiredKeyedService<Db2Collection<TKey, TRecord>>(key), lifetime));

        services.Add(new ServiceDescriptor(typeof(IVectorSearchable<TRecord>), serviceKey,
            static (sp, key) => sp.GetRequiredKeyedService<Db2Collection<TKey, TRecord>>(key), lifetime));

        return services;
    }

    /// <summary>
    /// Registers a <see cref="Db2Collection{TKey, TRecord}"/> as <see cref="VectorStoreCollection{TKey, TRecord}"/>, with the specified connection string and service lifetime.
    /// </summary>
    /// <inheritdoc cref="AddKeyedDb2Collection{TKey, TRecord}(IServiceCollection, object?, string, string, Db2CollectionOptions?, ServiceLifetime)"/>
    [RequiresUnreferencedCode("The Db2 provider is currently incompatible with trimming.")]
    [RequiresDynamicCode("The Db2 provider is currently incompatible with NativeAOT.")]
    public static IServiceCollection AddDb2Collection<TKey, TRecord>(
        this IServiceCollection services,
        string name,
        string connectionString,
        Db2CollectionOptions? options = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TKey : notnull
        where TRecord : class
        => AddKeyedDb2Collection<TKey, TRecord>(services, serviceKey: null, name, connectionString, options, lifetime);

    /// <summary>
    /// Registers a keyed <see cref="Db2Collection{TKey, TRecord}"/> as <see cref="VectorStoreCollection{TKey, TRecord}"/>, with the specified connection string and service lifetime.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="VectorStoreCollection{TKey, TRecord}"/> on.</param>
    /// <param name="serviceKey">The key with which to associate the collection.</param>
    /// <param name="name">The name of the collection.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="options">Options to further configure the collection.</param>
    /// <param name="lifetime">The service lifetime for the store. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>The service collection.</returns>
    [RequiresUnreferencedCode("The Db2 provider is currently incompatible with trimming.")]
    [RequiresDynamicCode("The Db2 provider is currently incompatible with NativeAOT.")]
    public static IServiceCollection AddKeyedDb2Collection<TKey, TRecord>(
        this IServiceCollection services,
        object? serviceKey,
        string name,
        string connectionString,
        Db2CollectionOptions? options = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TKey : notnull
        where TRecord : class
    {
        Throw.IfNullOrWhitespace(connectionString);

        return AddKeyedDb2Collection<TKey, TRecord>(services, serviceKey, name, _ => connectionString, _ => options!, lifetime);
    }

    private static Db2VectorStoreOptions? GetStoreOptions(IServiceProvider sp, Func<IServiceProvider, Db2VectorStoreOptions?>? optionsProvider)
    {
        var options = optionsProvider?.Invoke(sp);
        if (options?.EmbeddingGenerator is not null)
        {
            return options; // The user has provided everything, there is nothing to change.
        }

        var embeddingGenerator = sp.GetService<IEmbeddingGenerator>();
        return embeddingGenerator is null
            ? options // There is nothing to change.
            : new(options) { EmbeddingGenerator = embeddingGenerator }; // A copy, so the caller's options are not modified.
    }

    private static Db2CollectionOptions? GetCollectionOptions(IServiceProvider sp, Func<IServiceProvider, Db2CollectionOptions?>? optionsProvider)
    {
        var options = optionsProvider?.Invoke(sp);
        if (options?.EmbeddingGenerator is not null)
        {
            return options; // The user has provided everything, there is nothing to change.
        }

        var embeddingGenerator = sp.GetService<IEmbeddingGenerator>();
        return embeddingGenerator is null
            ? options // There is nothing to change.
            : new(options) { EmbeddingGenerator = embeddingGenerator }; // A copy, so the caller's options are not modified.
    }
}
