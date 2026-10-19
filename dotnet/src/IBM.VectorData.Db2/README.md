# IBM Db2 provider for Microsoft.Extensions.VectorData

`IBM.VectorData.Db2` implements the [Microsoft.Extensions.VectorData](https://learn.microsoft.com/dotnet/ai/vector-stores/overview) abstractions on IBM Db2, using the native `VECTOR` data type and `VECTOR_DISTANCE` function. You can use it with Microsoft Agent Framework, Semantic Kernel, or any other code that takes a `VectorStore`.

It is built on `IBM.Data.Db2.Vector`, a small client library for Db2 vectors that also works without MEVD.

> **Status:** preview. The package names are working names and are not published to NuGet yet.

## Requirements

- IBM Db2 **12.1.2** or later (the release that added `VECTOR` and `VECTOR_DISTANCE`).
- .NET 8, 9 or 10.
- The IBM Db2 .NET driver for your OS: `Net.IBM.Data.Db2` (Windows), `Net.IBM.Data.Db2-lnx` (Linux) or `Net.IBM.Data.Db2-osx` (macOS, including Apple Silicon).

## Getting started

```csharp
using IBM.VectorData.Db2;
using Microsoft.Extensions.VectorData;

var vectorStore = new Db2VectorStore("Server=myhost:50000;Database=MYDB;UID=user;PWD=password;");

var collection = vectorStore.GetCollection<int, Hotel>("hotels");
await collection.EnsureCollectionExistsAsync();

await collection.UpsertAsync(new Hotel { Id = 1, Name = "Seaside", Rating = 4.5, Embedding = embedding });

await foreach (var result in collection.SearchAsync(queryEmbedding, top: 3, new() { Filter = h => h.Rating > 4 }))
{
    Console.WriteLine($"{result.Record.Name}: {result.Score}");
}

public sealed class Hotel
{
    [VectorStoreKey]
    public int Id { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string Name { get; set; } = "";

    [VectorStoreData]
    public double Rating { get; set; }

    [VectorStoreVector(1536, DistanceFunction = DistanceFunction.CosineDistance)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
```

With dependency injection:

```csharp
services.AddDb2VectorStore(sp => configuration.GetConnectionString("Db2")!);
// or a single collection
services.AddDb2Collection<int, Hotel>("hotels", connectionString);
```

If an `IEmbeddingGenerator` is registered or set in the options, string properties marked with `[VectorStoreVector]` are embedded automatically, and `SearchAsync` accepts text.

## Distance functions

Db2 always returns a distance, where lower means more similar. The provider orders results by that distance and reports the score MEVD expects:

| `DistanceFunction` | Db2 metric | Score returned |
|---|---|---|
| `CosineDistance` (default) | `COSINE` | distance |
| `CosineSimilarity` | `COSINE` | 1 - distance |
| `NegativeDotProductSimilarity` | `DOT` | distance (Db2 returns the negated dot product) |
| `DotProductSimilarity` | `DOT` | -distance |
| `EuclideanDistance` | `EUCLIDEAN` | distance |
| `EuclideanSquaredDistance` | `EUCLIDEAN_SQUARED` | distance |
| `HammingDistance` | `HAMMING` | distance |
| `ManhattanDistance` | `MANHATTAN` | distance |

Records whose vector is `NULL` are never returned by vector search. A zero vector cannot be compared with `COSINE`: Db2 raises `SQL0801N` (division by zero).

## Type mapping

| .NET | Db2 |
|---|---|
| `int`, `long`, `short` | `INTEGER`, `BIGINT`, `SMALLINT` |
| `float`, `double`, `decimal` | `REAL`, `DOUBLE`, `DECIMAL(31,8)` |
| `string` | `VARCHAR(32672)`; `VARCHAR(1000)` for keys and indexed properties |
| `bool` | `BOOLEAN` |
| `Guid` | `CHAR(36)` |
| `DateTime`, `DateOnly`, `TimeOnly` | `TIMESTAMP(7)`, `DATE`, `TIME` |
| `byte[]` | `VARBINARY(32672)` |
| `string[]`, `List<string>` | `VARCHAR(32672)` holding a JSON array |
| `ReadOnlyMemory<float>`, `Embedding<float>`, `float[]` | `VECTOR(n, FLOAT32)` |

Keys can be `int`, `long`, `string` or `Guid`. `Guid` keys left empty are generated on upsert.

Not supported: `byte`, `DateTimeOffset` (Db2 has no time zone aware timestamp).

## Filtering

LINQ filters are translated to parameterized SQL: comparisons, `&&`, `||`, `!`, null checks, `Contains` over a list of values (`IN`), and `Contains`/`Any` over `string[]`/`List<string>` properties.

Comparisons follow SQL null semantics: `r => r.Name != "x"` does not match rows where `Name` is `NULL`. Add `r.Name == null ||` if you need those rows.

## Not supported yet

- Hybrid (keyword + vector) search with Db2 Text Search.
- Vector indexes (`CREATE VECTOR INDEX`, Db2 12.1.5+). Only `IndexKind.Flat` (exact search) is supported.
- `INT8` vectors.

## Testing

Unit tests run without a database:

```bash
dotnet test --project tests/IBM.VectorData.Db2.UnitTests
```

The [MEVD conformance suite](../../tests/IBM.VectorData.Db2.ConformanceTests) needs a Db2 12.1.2+ database. Set `Db2__ConnectionString`, or `DB2_HOST`, `DB2_PORT`, `DB2_NAME`, `DB2_USERNAME`, `DB2_PASSWORD` (and `DB2_SSL=true` for TLS), then run:

```bash
dotnet test --project tests/IBM.VectorData.Db2.ConformanceTests
```

## Acknowledgements

The filter translator and parts of the collection design are adapted from the SQL Server provider in [CommunityToolkit/AI](https://github.com/CommunityToolkit/AI) (MIT License, Copyright .NET Foundation and Contributors).
