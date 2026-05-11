# GenericFileServices

A .NET library that eliminates boilerplate when building file-based ETL pipelines. It covers both directions:

- **Import** — discover files, parse rows, reconcile with the database, archive or report errors.
- **Export** — fetch data from any EF Core source (table, view, stored procedure, table-valued function), format as a delimited file, and write to a local path or Azure Blob Storage.

Targets **net10.0**. Depends on [MagellanFileServices](https://github.com/DaveRogala/MagellanFileServices) for file I/O and [GenericRepositories](https://github.com/DaveRogala/GenericRepositories) for the EF Core repository pattern.

---

## Installation

```bash
dotnet add package GenericFileServices
```

---

## Import quick start

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
    string delimiter               = ",",
    bool firstLineContainsEncoding = false,
    bool failIfNotFound            = true,
    bool multipleFiles             = false,
    bool archiveIfSuccess          = true,
    bool hardDelete                = false,
    int  rowsToSkip                = 0,
    bool fixUnescapedQuotes        = false)
```

### Master overload (Azure Blob Storage)

```csharp
Task<List<string>> ProcessFileAsync(
    Stream stream,
    string blobConnectionString,
    string containerName,
    string filePath,
    Encoding encoding,
    string delimiter               = ",",
    bool firstLineContainsEncoding = false,
    bool failIfNotFound            = true,
    bool multipleFiles             = false,
    bool archiveIfSuccess          = true,
    bool hardDelete                = false,
    int  rowsToSkip                = 0,
    bool fixUnescapedQuotes        = false)
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

## Export quick start

### 1. Register services

```csharp
builder.Services.AddScoped<IFileWriterServices, FileWriterServices>();
builder.Services.AddScoped<IFileExportServices<Product, AppDbContext>,
                           FileExportServices<Product, AppDbContext>>();
```

### 2. Call it

The consuming application provides:
- A **data provider** delegate that fetches the data — any EF Core query form is supported.
- A **row mapper** that converts each record to an ordered sequence of string fields.
- The **headers**, **encoding** (default: UTF-8 without BOM), and **destination**.

**Table or view:**

```csharp
public class ProductExportJob(
    IFileExportServices<Product, AppDbContext> exporter,
    AppDbContext db)
{
    public async Task RunAsync()
    {
        List<string> errors = await exporter.ExportToFileAsync(
            basePath: @"C:\exports",
            fileName: $"products_{DateTime.UtcNow:yyyyMMdd}.csv",
            dataProvider: () => db.Products
                                  .Where(p => p.DateDeletedUtc == null)
                                  .ToListAsync(),
            rowMapper: p => [p.Sku, p.Name, p.Price.ToString("F2")],
            headers: ["SKU", "Name", "Price"],
            encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (errors.Count > 0)
            Console.WriteLine(string.Join('\n', errors));
    }
}
```

**Stored procedure:**

```csharp
List<string> errors = await exporter.ExportToFileAsync(
    basePath: @"C:\exports",
    fileName: "report.csv",
    dataProvider: () => db.Database
                          .SqlQuery<Product>($"EXEC dbo.GetActiveProducts")
                          .ToListAsync(),
    rowMapper: p => [p.Sku, p.Name, p.Price.ToString("F2")],
    headers: ["SKU", "Name", "Price"],
    encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
```

**Table-valued function:**

```csharp
List<string> errors = await exporter.ExportToFileAsync(
    basePath: @"C:\exports",
    fileName: "report.csv",
    dataProvider: () => db.Database
                          .SqlQuery<Product>($"SELECT * FROM dbo.GetProductsByCategory({categoryId})")
                          .ToListAsync(),
    rowMapper: p => [p.Sku, p.Name, p.Price.ToString("F2")],
    headers: ["SKU", "Name", "Price"],
    encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
```

**Azure Blob Storage:**

```csharp
List<string> errors = await exporter.ExportToBlobAsync(
    blobConnectionString: connectionString,
    containerName: "exports",
    blobPath: $"products/products_{DateTime.UtcNow:yyyyMMdd}.csv",
    dataProvider: () => db.Products.ToListAsync(),
    rowMapper: p => [p.Sku, p.Name, p.Price.ToString("F2")],
    headers: ["SKU", "Name", "Price"],
    encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
```

---

## ExportToFileAsync / ExportToBlobAsync reference

Both methods return `Task<List<string>>` — an empty list means success. Argument errors (`basePath`, `fileName`, `blobConnectionString`, etc.) throw `ArgumentException`; data-provider and write failures are caught and returned in the list.

### Local file system

```csharp
Task<List<string>> ExportToFileAsync(
    string basePath,
    string fileName,
    Func<Task<List<T>>> dataProvider,
    Func<T, IEnumerable<string>> rowMapper,
    IEnumerable<string> headers,
    Encoding encoding,
    string delimiter = ",")
```

### Azure Blob Storage

```csharp
Task<List<string>> ExportToBlobAsync(
    string blobConnectionString,
    string containerName,
    string blobPath,
    Func<Task<List<T>>> dataProvider,
    Func<T, IEnumerable<string>> rowMapper,
    IEnumerable<string> headers,
    Encoding encoding,
    string delimiter = ",")
```

### Parameter reference

| Parameter | Default | Description |
|---|---|---|
| `basePath` | — | Target directory (local or UNC). Must exist. |
| `fileName` | — | Output file name, e.g. `"products_20260101.csv"` |
| `blobConnectionString` | — | Azure Storage connection string |
| `containerName` | — | Target blob container name |
| `blobPath` | — | Full blob path, e.g. `"exports/products_20260101.csv"`. Overwrites if exists. |
| `dataProvider` | — | Async delegate that returns the data set |
| `rowMapper` | — | Maps one record to an ordered sequence of string field values |
| `headers` | — | Column header names written as the first row. Empty sequence omits the header. |
| `encoding` | — | Character encoding. UTF-8 without BOM recommended. |
| `delimiter` | `","` | Column delimiter |

### Field quoting

`FileWriterServices` applies RFC 4180 quoting automatically. A field is wrapped in double-quotes when it contains the delimiter, a double-quote character, or a line terminator. Double-quote characters within a quoted field are escaped by doubling: `"`.

---

## BaseObject

All import entities must inherit `BaseObject`, which provides soft-delete timestamps and a surrogate key:

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

Export data types do not need to extend `BaseObject` — the `T` in `IFileExportServices<T, C>` is constrained only to `class`.

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
