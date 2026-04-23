# GenericFileImportServices

A .NET library that eliminates boilerplate when building file-to-database ETL pipelines. Consumers extend a single abstract base class, implement three reconciliation methods, and the library handles file discovery, parsing, database upsert/delete, archiving, and error handling.

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

Extend `FileImportServices<TEntity, TDto, TContext>` and implement the three reconciliation methods:

```csharp
public class ProductImportService(
    IDatabaseServices<Product, AppDbContext> db,
    IFileReaderServices<ProductDto> reader,
    ILogger<ProductImportService> logger)
    : FileImportServices<Product, ProductDto, AppDbContext>(db, reader, logger)
{
    public override List<Product> GetAddEntities(List<Product> existing, List<ProductDto> dtos) =>
        dtos.Where(d => existing.All(e => e.Sku != d.Sku))
            .Select(d => new Product { Sku = d.Sku, Name = d.Name, Price = d.Price })
            .ToList();

    public override List<Product> GetUpdateEntities(List<Product> existing, List<ProductDto> dtos) =>
        existing.Where(e => dtos.Any(d => d.Sku == e.Sku))
                .Select(e => { var d = dtos.First(d => d.Sku == e.Sku); e.Name = d.Name; e.Price = d.Price; return e; })
                .ToList();

    public override List<Product> GetDeleteEntities(List<Product> existing, List<ProductDto> dtos) =>
        existing.Where(e => e.DateDeletedUtc is null && dtos.All(d => d.Sku != e.Sku))
                .ToList();
}
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
4. **Reconcile** by calling your three abstract methods:
   - `GetAddEntities` — rows in the file not yet in the database
   - `GetUpdateEntities` — rows in both; apply field changes to the existing entities
   - `GetDeleteEntities` — rows in the database no longer in the file
5. **Persist** adds, updates, and deletes in a single `SaveChanges` call
6. **Archive or error** the file via `MagellanFileServices`

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
