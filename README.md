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

### 1. Define an export DTO

Define a record (or class) that represents a single output row. CsvHelper derives column headers and field formatting from the type's properties. Use `[Name]` to customise a column header, `[Index]` to fix column order, or a `ClassMap<T>` for more control.

```csharp
using CsvHelper.Configuration.Attributes;

public record ProductExportDto(
    [property: Name("SKU")]          string Sku,
    [property: Name("Product Name")] string Name,
    [property: Name("Price")]        decimal Price);
```

### 2. Register services

```csharp
// With DbContext configuration:
builder.Services.AddFileExportServices<ProductExportDto, AppDbContext>(
    options => options.UseSqlServer(connectionString));

// Or when DbContextFactory is already registered:
builder.Services.AddFileExportServices<ProductExportDto, AppDbContext>();
```

### 3. Call it

Provide an async **data provider** delegate that returns `List<T>`. Any EF Core query form is supported — the library does not constrain the query shape.

**Table or view:**

```csharp
public class ProductExportJob(
    IFileExportServices<ProductExportDto, AppDbContext> exporter,
    AppDbContext db)
{
    public async Task RunAsync()
    {
        List<string> errors = await exporter.ExportToFileAsync(
            basePath: @"C:\exports",
            fileName: $"products_{DateTime.UtcNow:yyyyMMdd}.csv",
            dataProvider: () => db.Products
                                  .Where(p => p.DateDeletedUtc == null)
                                  .Select(p => new ProductExportDto(p.Sku, p.Name, p.Price))
                                  .ToListAsync(),
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
                          .SqlQuery<ProductExportDto>($"EXEC dbo.GetActiveProducts")
                          .ToListAsync(),
    encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
```

**Table-valued function:**

```csharp
List<string> errors = await exporter.ExportToFileAsync(
    basePath: @"C:\exports",
    fileName: "report.csv",
    dataProvider: () => db.Database
                          .SqlQuery<ProductExportDto>(
                              $"SELECT * FROM dbo.GetProductsByCategory({categoryId})")
                          .ToListAsync(),
    encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
```

> **SQL injection note:** Pass the interpolated string literal directly to `SqlQuery` / `FromSql` — do **not** pre-evaluate it to a plain `string` variable first. EF Core intercepts the `FormattableString` and binds each `{value}` as a `DbParameter`. If you assign `var sql = $"...{categoryId}..."` and then pass `sql`, EF Core receives a plain string and cannot parameterise it, making the query vulnerable.

**Archive an existing file before overwriting:**

When `archiveExistingFile: true`, if the target file already exists it is timestamped and moved to the archive directory before the new file is written. The archive directory is created automatically.

```csharp
// Default: archive sub-folder of basePath
await exporter.ExportToFileAsync(..., archiveExistingFile: true);

// Absolute archive path
await exporter.ExportToFileAsync(..., archiveExistingFile: true,
    archivePath: @"D:\archive\exports");

// Relative archive path (resolved relative to basePath)
await exporter.ExportToFileAsync(..., archiveExistingFile: true,
    archivePath: "old");
```

The archived file is named `<stem>_<yyyyMMddHHmmssfff><ext>` — for example, `products_20260511143022123.csv`.

**Azure Blob Storage:**

```csharp
List<string> errors = await exporter.ExportToBlobAsync(
    blobConnectionString: connectionString,
    containerName: "exports",
    blobPath: $"products/products_{DateTime.UtcNow:yyyyMMdd}.csv",
    dataProvider: () => db.Products
                          .Select(p => new ProductExportDto(p.Sku, p.Name, p.Price))
                          .ToListAsync(),
    encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
```

**Archive an existing blob before overwriting:**

When `archiveExistingBlob: true`, if the target blob already exists it is copied to the archive path with a timestamp appended to the file stem, and the original is deleted before the new blob is uploaded. The archive path is a blob path prefix within the same container.

```csharp
// Default: exports/archive/products_20260512143022123.csv
await exporter.ExportToBlobAsync(..., archiveExistingBlob: true);

// Custom archive path
await exporter.ExportToBlobAsync(..., archiveExistingBlob: true,
    archivePath: "archive/products");
```

**Metadata header:**

When the consuming system requires descriptive lines at the top of the file before the column headers (e.g. extract date, source system, row count), supply a dictionary keyed by line order. Lines are written in ascending key order; gaps in the key sequence are ignored.

```csharp
var metadata = new Dictionary<int, string>
{
    { 1, "Source: ERP" },
    { 2, $"ExtractDate: {DateTime.UtcNow:yyyy-MM-dd}" },
    { 3, "Version: 2" }
};

await exporter.ExportToFileAsync(
    basePath: @"C:\exports",
    fileName: "products.csv",
    dataProvider: ...,
    encoding: new UTF8Encoding(false),
    metadataHeader: metadata);
```

Output line order: metadata lines → encoding line (if `writeEncodingHeader: true`) → CSV column header → data rows.

**Full CsvHelper control (`CsvConfiguration` overload):**

When the convenience parameters (`delimiter`, `writeHeader`) are not enough — for example, to force quoting on specific columns — pass a `CsvConfiguration` directly. This overload omits `delimiter` and `writeHeader`; set those on the configuration object instead.

```csharp
using CsvHelper.Configuration;
using System.Globalization;

var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Delimiter = ",",
    HasHeaderRecord = true,
    ShouldQuote = args => args.MemberMapData?.Member?.Name == nameof(ProductExportDto.Sku)
};

await exporter.ExportToFileAsync(
    basePath: @"C:\exports",
    fileName: "products.csv",
    dataProvider: ...,
    encoding: new UTF8Encoding(false),
    csvConfiguration: config);
```

The same overload is available on `ExportToBlobAsync`. All other optional parameters (`archiveExistingFile`, `archivePath`, `metadataHeader`, `writeEncodingHeader`, `encodingHeaderOverride`) are still available in both overloads.

---

## ExportToFileAsync / ExportToBlobAsync reference

Both methods return `Task<List<string>>` — an empty list means success. Argument errors (`basePath`, `fileName`, `blobConnectionString`, etc.) throw `ArgumentException`; data-provider and write failures are caught and returned in the list.

### Local file system

```csharp
Task<List<string>> ExportToFileAsync(
    string basePath,
    string fileName,
    Func<Task<List<T>>> dataProvider,
    Encoding encoding,
    string delimiter                           = ",",
    bool archiveExistingFile                   = false,
    string? archivePath                        = null,
    IReadOnlyDictionary<int, string>? metadataHeader = null,
    bool writeHeader                           = true,
    bool writeEncodingHeader                   = false,
    string? encodingHeaderOverride             = null)
```

### Azure Blob Storage

```csharp
Task<List<string>> ExportToBlobAsync(
    string blobConnectionString,
    string containerName,
    string blobPath,
    Func<Task<List<T>>> dataProvider,
    Encoding encoding,
    string delimiter                           = ",",
    bool archiveExistingBlob                   = false,
    string? archivePath                        = null,
    IReadOnlyDictionary<int, string>? metadataHeader = null,
    bool writeHeader                           = true,
    bool writeEncodingHeader                   = false,
    string? encodingHeaderOverride             = null)
```

### CsvConfiguration overloads

When you need full control over CsvHelper — custom quoting, class maps, culture settings, etc. — use the `CsvConfiguration` overloads. These replace `delimiter` and `writeHeader` with a single `CsvConfiguration` parameter; all other parameters remain available.

```csharp
// Local file system
Task<List<string>> ExportToFileAsync(
    string basePath,
    string fileName,
    Func<Task<List<T>>> dataProvider,
    Encoding encoding,
    CsvConfiguration csvConfiguration,
    bool archiveExistingFile                   = false,
    string? archivePath                        = null,
    IReadOnlyDictionary<int, string>? metadataHeader = null,
    bool writeEncodingHeader                   = false,
    string? encodingHeaderOverride             = null)

// Azure Blob Storage
Task<List<string>> ExportToBlobAsync(
    string blobConnectionString,
    string containerName,
    string blobPath,
    Func<Task<List<T>>> dataProvider,
    Encoding encoding,
    CsvConfiguration csvConfiguration,
    bool archiveExistingBlob                   = false,
    string? archivePath                        = null,
    IReadOnlyDictionary<int, string>? metadataHeader = null,
    bool writeEncodingHeader                   = false,
    string? encodingHeaderOverride             = null)
```

### Parameter reference

| Parameter | Default | Description |
|---|---|---|
| `basePath` | — | Target directory (local or UNC). Must exist. |
| `fileName` | — | Output file name, e.g. `"products_20260101.csv"` |
| `blobConnectionString` | — | Azure Storage connection string |
| `containerName` | — | Target blob container name |
| `blobPath` | — | Full blob path, e.g. `"exports/products_20260101.csv"`. Overwrites if exists. |
| `dataProvider` | — | Async delegate that returns `List<T>` |
| `encoding` | — | Character encoding. UTF-8 without BOM recommended. |
| `delimiter` | `","` | Column delimiter *(convenience overload only)* |
| `csvConfiguration` | — | Full CsvHelper configuration *(CsvConfiguration overload only)*. Caller sets `Delimiter`, `HasHeaderRecord`, `ShouldQuote`, etc. |
| `archiveExistingFile` | `false` | Move an existing **local** file at the target path to the archive directory before writing |
| `archiveExistingBlob` | `false` | Copy an existing **blob** at the target path to the archive path, then delete it, before uploading |
| `archivePath` | `null` | Archive location. For files: absolute paths used as-is; relative paths resolved relative to `basePath`; `null` defaults to `archive` sub-folder of `basePath`. For blobs: blob path prefix within the same container; `null` defaults to `archive` folder inside the blob's current directory (e.g. `exports/archive`). |
| `metadataHeader` | `null` | Optional lines written at the top of the file before the encoding line and CSV column header. Keys determine the output order (ascending); gaps in the key sequence are ignored. When `null`, no metadata lines are written. |
| `writeHeader` | `true` | When `false`, suppresses the CSV column header row. *(convenience overload only — use `HasHeaderRecord` on the configuration object in the CsvConfiguration overload)* |
| `writeEncodingHeader` | `false` | When `true`, writes the encoding as a line after any metadata and before the CSV header row |
| `encodingHeaderOverride` | `null` | Custom string to write as the encoding line. When `null`, defaults to `Encoding.WebName` (e.g. `"utf-8"`, `"utf-16"`). Ignored unless `writeEncodingHeader` is `true`. |

### Column mapping

CsvHelper derives column headers and field values directly from `T`. Use attributes from `CsvHelper.Configuration.Attributes` on your record properties, or register a `ClassMap<T>` with the CsvHelper configuration for more complex mappings.

| Attribute | Purpose |
|---|---|
| `[Name("Column Header")]` | Override the column header |
| `[Index(0)]` | Fix the column position |
| `[Ignore]` | Exclude a property from the output |
| `[Format("F2")]` | Apply a format string to the value |

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
