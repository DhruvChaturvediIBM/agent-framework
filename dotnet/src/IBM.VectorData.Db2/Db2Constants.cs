// Copyright (c) IBM Corporation. All rights reserved.

namespace IBM.VectorData.Db2;

internal static class Db2Constants
{
    internal const string VectorStoreSystemName = "ibm.db2";

    // Db2 allows far more parameter markers per statement (32767); batches stay small to keep statements cacheable.
    internal const int MaxParameterCount = 1_000;

    // Longest string key / indexed string column. A Db2 index key on a 4K page is limited to 1024 bytes.
    internal const int MaxIndexedStringLength = 1_000;

    // Longest VARCHAR / VARBINARY column.
    internal const int MaxStringLength = 32_672;
}
