# GenericFileImportServices

A .NET library that eliminates boilerplate when building file-to-database ETL pipelines. Consumers extend a single base class, override only the reconciliation methods they need, and the library handles file discovery, parsing, database upsert/delete, archiving, and error handling.

Targets **net10.0**. Depends on [MagellanFileServices](https://github.com/DaveRogala/MagellanFileServices) for file I/O and [GenericRepositories](https://github.com/DaveRogala/GenericRepositories) for the EF Core repository pattern.

---

## Installation

```bash
dotnet add package GenericFileImportServices
```

---

## Quick start

### 1. Define your models

Your database entity must extend `BaseObject`:

```csharp
public class Product : BaseObject
{
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
}
```

Your DTO (the parsed row type) can be any class:

```csharp
public class ProductDto
{
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}
```

### 2. Implement the import service

Extend `FileImportServices<TEntity, TDto, TContext>` and override only the reconciliation methods your scenario requires. All three default to a no-op (empty list), so unneeded operations require no code at all.

**Add, update, and delete:**

```csharp
public class ProductImportService(
    IDatabaseServices<Product, AppDbContext> db,
    IFileReaderServices<ProductDto> reader,
    ILogger<ProductImportService> logger)
    : FileImportServices<Product, ProductDto, AppDbContext>(db, reader, logger)
{
    public override List<Product> GetAddEntities(List<Product> existing, List<ProductDto> dtos)
    {
        var existingSkus = existing.Select(e => e.Sku).ToHashSet();
        return dtos.Where(d => !existingSkus.Contains(d.Sku))
                   .Select(d => new Product { Sku = d.Sku, Name = d.Name, Price = d.Price })
                   .ToList();
    }

    public override List<Product> GetUpdateEntities(List<Product> existing, List<ProductDto> dtos)
    {
        var dtosBySku = dtos.ToDictionary(d => d.Sku);
        return existing.Where(e => dtosBySku.ContainsKey(e.Sku))
                       .Select(e => { var d = dtosBySku[e.Sku]; e.Name = d.Name; e.Price = d.Price; return e; })
                       .ToList();
    }

    public override List<Product> GetDeleteEntities(List<Product> existing, List<ProductDto> dtos)
    {
        var dtoSkus = dtos.Select(d => d.Sku).ToHashSet();
        return existing.Where(e => e.DateDeletedUtc is null && !dtoSkus.Contains(e.Sku))
                       .ToList();
    }
}
```

**Add and delete only** — omit `GetUpdateEntities` entirely:

```csharp
public class ProductImportService(...)
    : FileImportServices<Product, ProductDto, AppDbContext>(...)
{
    public override List<Product> GetAddEntities(List<Product> existing, List<ProductDto> dtos) =>
        dtos.Where(d => existing.All(e => e.Sku != d.Sku))
            .Select(d => new Product { Sku = d.Sku, Name = d.Name, Price = d.Price })
            .ToList();

    public override List<Product> GetDeleteEntities(List<Product> existing, List<ProductDto> dtos) =>
        existing.Where(e => e.DateDeletedUtc is null && dtos.All(d => d.Sku != e.Sku))
                .ToList();
}
```

**Append-only** — override nothing:

```csharp
public class ProductImportService(...)
    : FileImportServices<Product, ProductDto, AppDbContext>(...) { }
```

### 3. Register services

```csharp
// With DbContext configuration:
builder.Services.AddFileImportServices<Product, ProductDto, AppDbContext, ProductImportService>(
    options => options.UseSqlServer(connectionString));

// Or when DbContextFactory is already registered:
builder.Services.AddFileImportServices<Product, ProductDto, AppDbContext, ProductImportService>();
```

### 4. Call it

```csharp
public class MyJob(IFileImportServices<Product, ProductDto, AppDbContext> importer)
{
    public async Task RunAsync()
    {
        // Local file system
        List<string> errors = await importer.ProcessFileAsync(
            basePath: @"C:\imports",
            fileNamePattern: "products_*.csv");

        // Azure Blob Storage
        List<string> errors = await importer.ProcessFileAsync(
            stream: blobStream,
            blobConnectionString: connectionString,
            containerName: "imports",
            filePath: "products/products_20260101.csv",
            encoding: Encoding.UTF8);

        if (errors.Count > 0)
            Console.WriteLine(string.Join('\n', errors));
    }
}
```

---

## ProcessFileAsync reference

All overloads return `Task<List<string>>` — an empty list means success; entries are per-row or per-file error messages.

### Master overload (local file system)

```csharp
Task<List<string>> ProcessFileAsync(
    string basePath,
    string fileNamePattern,
    Encoding encoding,
    string delimiter              = ",",
    bool firstLineContainsEncoding = false,
    bool failIfNotFound           = true,
    bool multipleFiles            = false,
    bool archiveIfSuccess         = true,
    bool hardDelete               = false,
    int  rowsToSkip               = 0,
    bool fixUnescapedQuotes       = false)
```

### Master overload (Azure Blob Storage)

```csharp
Task<List<string>> ProcessFileAsync(
    Stream stream,
    string blobConnectionString,
    string containerName,
    string filePath,
    Encoding encoding,
    string delimiter              = ",",
    bool firstLineContainsEncoding = false,
    bool failIfNotFound           = true,
    bool multipleFiles            = false,
    bool archiveIfSuccess         = true,
    bool hardDelete               = false,
    int  rowsToSkip               = 0,
    bool fixUnescapedQuotes       = false)
```

### Parameter reference

| Parameter | Default | Description |
|---|---|---|
| `basePath` | — | Directory to search for files |
| `fileNamePattern` | — | Wildcard pattern, e.g. `"products_*.csv"` |
| `encoding` | `Encoding.Default` | File character encoding |
| `delimiter` | `","` | Column delimiter |
| `firstLineContainsEncoding` | `false` | Whether the first data line is a header row |
| `failIfNotFound` | `true` | Throw if no files match the pattern |
| `multipleFiles` | `false` | Allow more than one matching file |
| `archiveIfSuccess` | `true` | Move/rename file after a clean import |
| `hardDelete` | `false` | Permanently delete; `false` sets `DateDeletedUtc` (soft delete) |
| `rowsToSkip` | `0` | Number of leading rows to skip before parsing (e.g. metadata headers) |
| `fixUnescapedQuotes` | `false` | Attempt to repair unescaped quote characters in CSV fields |

### Convenience overloads

```csharp
// Simplest — defaults for everything
ProcessFileAsync(basePath, fileNamePattern)

// Skip N leading rows
ProcessFileAsync(basePath, fileNamePattern, rowsToSkip: 3)

// Skip rows and fix unescaped quotes
ProcessFileAsync(basePath, fileNamePattern, rowsToSkip: 1, fixUnescapedQuotes: true)

// Fix unescaped quotes only (named param on master overload)
ProcessFileAsync(basePath, fileNamePattern, fixUnescapedQuotes: true)

// Hard delete
ProcessFileAsync(basePath, fileNamePattern, hardDelete: true)
```

---

## How reconciliation works

Each call to `ProcessFileAsync` follows this sequence:

1. **Discover** files matching `fileNamePattern` in `basePath`
2. **Parse** each file into `List<TDto>` via `MagellanFileServices`
3. **Load** all existing `TEntity` records from the database (once, before the file loop)
4. **Reconcile** by calling the three virtual methods (override only what you need; unoverridden methods return an empty list):
   - `GetAddEntities` — rows in the file not yet in the database
   - `GetUpdateEntities` — rows in both; apply field changes to the existing entities
   - `GetDeleteEntities` — rows in the database no longer in the file
5. **Persist** adds, updates, and deletes in a single `SaveChanges` call
6. **Archive or error** the file via `MagellanFileServices`

---

## Reconciliation performance

The three methods are called once per file. A naive implementation using `.All()` or `.Any()` to search the list on every iteration produces **O(n²)** comparisons — measurable in practice:

| Method | N = 100 | N = 1 000 | N = 10 000 |
|---|--:|--:|--:|
| `GetAddEntities` (linear scan) | 9.6 µs | 726 µs | **93.5 ms** |
| `GetDeleteEntities` (linear scan) | 7.0 µs | 598 µs | **63.4 ms** |

Build a `HashSet<string>` (or `Dictionary<TKey, TValue>`) from the key field before the scan. Lookups then cost O(1), making the whole method O(n):

```csharp
// ❌ O(n²) — searches the full existing list for every DTO
public override List<Product> GetAddEntities(List<Product> existing, List<ProductDto> dtos) =>
    dtos.Where(d => existing.All(e => e.Sku != d.Sku))
        .Select(d => new Product { Sku = d.Sku, Name = d.Name, Price = d.Price })
        .ToList();

// ✅ O(n) — one HashSet.Contains call per DTO
public override List<Product> GetAddEntities(List<Product> existing, List<ProductDto> dtos)
{
    var existingSkus = existing.Select(e => e.Sku).ToHashSet();
    return dtos.Where(d => !existingSkus.Contains(d.Sku))
               .Select(d => new Product { Sku = d.Sku, Name = d.Name, Price = d.Price })
               .ToList();
}
```

The same pattern applies to each method:

| Method | Build from | Lookup target |
|---|---|---|
| `GetAddEntities` | `HashSet` of existing entity keys | each DTO key |
| `GetUpdateEntities` | `Dictionary<key, TDto>` | each existing entity key |
| `GetDeleteEntities` | `HashSet` of DTO keys | each existing entity key |

If your key field is a string, pass `StringComparer.OrdinalIgnoreCase` to `ToHashSet` / `ToDictionary` when case-insensitive matching is needed.

---

## File import metadata

Optionally track which file produced which rows by enabling import metadata recording. Two tables are created in your database: `FileImportRecord` (one row per file) and `FileImportEntityLink` (one row per entity touched by the import).

### 1. Configure the DbContext

```csharp
using GenericFileImportServices.Extensions;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddFileImportMetadata();
    }
}
```

### 2. Register the metadata service

Call `AddFileImportMetadataServices` **after** `AddFileImportServices`:

```csharp
builder.Services.AddFileImportServices<Product, ProductDto, AppDbContext, ProductImportService>(
    options => options.UseSqlServer(connectionString));

builder.Services.AddFileImportMetadataServices<AppDbContext>();
```

### 3. Inject into the import service

Add `IFileImportRecordServices<AppDbContext>` as an optional constructor parameter and pass it through to the base class:

```csharp
public class ProductImportService(
    IDatabaseServices<Product, AppDbContext> db,
    IFileReaderServices<ProductDto> reader,
    ILogger<ProductImportService> logger,
    IFileImportRecordServices<AppDbContext>? metadataServices = null)
    : FileImportServices<Product, ProductDto, AppDbContext>(db, reader, logger, metadataServices)
{
    // reconciliation overrides as before
}
```

After each successful import, `ProcessFileAsync` writes one `FileImportRecord` and one `FileImportEntityLink` per entity that was added or updated. No metadata is written when `archiveIfSuccess` is `false` or when the file contains parse errors.

### Metadata schema

| Table | Column | Notes |
|---|---|---|
| `FileImportRecord` | `Id` | Surrogate PK |
| | `ImportFileName` | Original file name (e.g. `orders_20260101.csv`) |
| | `ArchivedFileName` | Archive name after move (e.g. `orders_20260101_20260101120000000.csv`) |
| | `DateTimeAddedUtc` | UTC timestamp of the import |
| `FileImportEntityLink` | `FileImportRecordId` | FK → `FileImportRecord.Id` (cascade delete) |
| | `EntityId` | Logical FK to the entity's `Id`; no EF navigation (entity type is generic) |

`FileImportEntityLink` uses a composite PK on `(FileImportRecordId, EntityId)`. `EntityId` is indexed for reverse lookups. Consumers who want a database-enforced FK from `EntityId` to their entity table can add it in `OnModelCreating`:

```csharp
modelBuilder.Entity<FileImportEntityLink>()
    .HasOne<Product>()
    .WithMany()
    .HasForeignKey(l => l.EntityId)
    .OnDelete(DeleteBehavior.Cascade);
```

---

## BaseObject

All entities must inherit `BaseObject`, which provides soft-delete timestamps and a surrogate key:

```csharp
public class BaseObject
{
    public int Id { get; set; }
    public required DateTime DateAddedUtc { get; set; }
    public required DateTime DateUpdatedUtc { get; set; }
    public DateTime? DateDeletedUtc { get; set; }   // null = active
}
```

`DateDeletedUtc` is indexed automatically. Soft-deletes set this field; hard-deletes remove the row.

---

## Extending the file reader

Override `FileReaderServices<U>` if you need custom parsing logic (e.g. fixed-width files):

```csharp
public class MyFileReaderServices(IFileServices fileServices, ILogger<MyFileReaderServices> logger)
    : FileReaderServices<MyDto>(fileServices, logger)
{
    public override List<FileResults<MyDto>> ReadFromFile(
        string basePath, string fileNamePattern, Encoding encoding,
        string delimiter = ",", bool firstLineContainsEncoding = false,
        bool failIfFileMissing = true, bool multipleFiles = false,
        int rowsToSkip = 0, bool fixUnescapedQuotes = false)
    {
        // custom logic
    }
}
```

Register your custom reader by replacing the default in DI after calling `AddFileImportServices`, or manage registrations manually.

---

## License

MIT — see [LICENSE](LICENSE).
