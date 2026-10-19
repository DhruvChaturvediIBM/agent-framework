// Copyright (c) IBM Corporation. All rights reserved.

using System;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Extensions.VectorData;

namespace IBM.VectorData.Db2;

/// <summary>
/// Wraps database errors in <see cref="VectorStoreException"/>, as the MEVD contract requires.
/// </summary>
internal static class Db2ErrorHandler
{
    internal static async Task<TResult> RunAsync<TResult>(VectorStoreCollectionMetadata metadata, string operationName, Func<Task<TResult>> operation)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (DbException ex)
        {
            throw Wrap(ex, metadata, operationName);
        }
    }

    internal static async Task RunAsync(VectorStoreCollectionMetadata metadata, string operationName, Func<Task> operation)
    {
        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (DbException ex)
        {
            throw Wrap(ex, metadata, operationName);
        }
    }

    internal static VectorStoreException Wrap(DbException ex, VectorStoreCollectionMetadata metadata, string operationName)
        => new("Call to vector store failed.", ex)
        {
            VectorStoreSystemName = metadata.VectorStoreSystemName,
            VectorStoreName = metadata.VectorStoreName,
            CollectionName = metadata.CollectionName,
            OperationName = operationName,
        };
}
