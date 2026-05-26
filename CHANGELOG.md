# Changelog

All notable changes to GenericFileServices are documented here.

## [Unreleased]

### New features

- **`FileImportOptions.FileHasHeader`** — new `bool` property (default `true`). Set to `false` to import headerless CSV files; CsvHelper's `[Index]` attributes on the DTO type then determine column order. The underlying `IFileServices` delegation is bypassed in the headerless path; CsvHelper reads directly with `HasHeaderRecord = false`.
- **`IFileReaderServices.ReadFromFile` — `fileHasHeader` parameter** — both the file-system and stream overloads now accept a trailing `bool fileHasHeader = true` parameter. Existing call sites are unaffected; pass `fileHasHeader: false` to activate headerless parsing.
- **`FileImportOptions.FailIfNoRecords`** — new `bool` property (default `false`). Set to `true` for source-of-truth files that must never be empty; a parsed file with zero data records throws an `InvalidOperationException`, which is caught per-file and routes to `HandleFileError`.
- **`FileImportOptions.ErrorThresholdPercentage`** — new `double?` property (default `null`). When set (0–100), the import is aborted and no database updates are made if the number of adds **or** deletes exceeds this percentage of the current active record count. The error is caught per-file and routes to `HandleFileError`.
- **`FileImportOptions.WarningThresholdPercentage`** — new `double?` property (default `null`). Same calculation as `ErrorThresholdPercentage` but only logs a warning via `ILogger`; the import proceeds normally. Both thresholds may be set simultaneously — the error check runs first.
- **Cancellation token support** — all async methods in `IFileImportServices` (canonical `FileImportOptions` overloads only), `IFileReaderServices`, `IFileWriterServices`, `IFileExportServices`, and `IDatabaseServices` now accept an optional `CancellationToken cancellationToken = default` parameter. Existing call sites are unaffected. Tokens are propagated to Azure Storage SDK calls (`UploadAsync`, `ExistsAsync`, `SyncCopyFromUriAsync`, `DeleteAsync`) and to EF Core repository calls (`AllAsync`, `FindAsync`, `AddAsync`, `SaveChangesAsync`). The `[Obsolete]` overloads on `IFileImportServices` do not receive the token.

---

## [3.0.0] - 2026-05-13

### Breaking changes

- **`IBlobClientFactory`** — new method `GetContainerClient(string connectionString, string containerName)` added to the interface. Any class that directly implements `IBlobClientFactory` must add this method.
- **`FileReaderServices<U>` constructor** — now requires an `IBlobClientFactory` parameter:
  ```csharp
  public FileReaderServices(IFileServices fileServices, ILogger<...> logger, IBlobClientFactory blobClientFactory)
  ```
  Subclasses and manual DI registrations that constructed `FileReaderServices` directly must pass the factory. `AddFileImportServices` handles this automatically.
- **`IFileImportServices` overloads deprecated** — the nine individual boolean-parameter overloads are now marked `[Obsolete]`. They continue to work but will emit a compiler warning. Migrate to the new `FileImportOptions` overloads (see below).

### New features

- **`FileImportOptions` record** — collapses all nine import optional parameters into a single init-only record. Pass it to the two new canonical `ProcessFileAsync` overloads:
  ```csharp
  await importer.ProcessFileAsync(basePath, fileNamePattern, new FileImportOptions
  {
      Delimiter = "\t",
      HardDelete = true,
      RowsToSkip = 1
  });
  ```
  `FileImportOptions.Default` holds an instance with all defaults pre-set.
- **`IFileExportServices<T>`** — new non-generic interface. `IFileExportServices<T, C>` now extends it. New code can inject `IFileExportServices<T>` directly, removing the unnecessary `DbContext` type parameter from injection sites. Existing `IFileExportServices<T, C>` registrations continue to work without change.
- **`IBlobClientFactory.GetContainerClient`** — new method added; `BlobClientFactory` caches `BlobContainerClient` instances by `(connectionString, containerName)`, eliminating repeated HTTP pipeline construction in batch export scenarios.

### Security fixes

- **Path traversal guard** — `WriteToFile` and `ArchiveExistingFile` now validate that the resolved path stays within `basePath`. An absolute `fileName` or a `..`-relative name that escapes the root throws `ArgumentException`.
- **Metadata newline sanitisation** — newline characters in metadata header values are replaced with a space before being written, preventing row injection.
- **Timestamp precision** — archive file name timestamp format changed from `yyyyMMddHHmmssfff` (milliseconds) to `yyyyMMddHHmmssfffffff` (100-nanosecond ticks), eliminating the theoretical collision window under high-frequency archiving.

### Performance improvements

- **Server-side blob archive** — `ArchiveExistingBlobAsync` now uses `SyncCopyFromUriAsync` + `DeleteAsync` instead of a full download-to-`MemoryStream`-then-re-upload round trip. Memory use is constant regardless of blob size.
- **Empty-file short-circuit** — `ProcessFileAsync` skips `GetAllEntitiesAsync` (and the resulting full-table read) when the parsed file contains no object results.
- **Default `CsvConfiguration` cached** — the convenience `WriteToFile` / `WriteToBlobAsync` overloads reuse a `static readonly` `CsvConfiguration` for the common case (comma delimiter, header row), avoiding a per-call allocation.

### Internal improvements

- `TimestampFormat` extracted to a shared `internal static class Constants`, eliminating duplication between `FileImportServices` and `FileWriterServices`.
- `private protected` accessibility on `FileReaderServices` fields narrowed to `private`.
- `static readonly string` constants in `FileImportServices` and `FileWriterServices` changed to `const string`.
- `CsvWriter` in `WriteToBlobAsync` changed to `await using` with an explicit `await csv.FlushAsync()` before `StreamWriter` disposes.
- All bare `/// <inheritdoc/>` on `FileImportServices` updated to use explicit `cref` attributes.

---

## [2.1.0] - 2026-04-24

### New features

- **`GetAddEntities`, `GetUpdateEntities`, `GetDeleteEntities`** — changed from `abstract` to `virtual` with a no-op default (`=> []`). Consumers no longer need to override all three; unneeded operations can simply be omitted rather than returning an empty list explicitly. Consumers who already override all three see no change in behaviour.

---

## [2.0.0] - 2026-04-23

### Breaking changes

- **`IDatabaseServices.UpdateDatabaseAsync`** — removed the vestigial `existingEntities` parameter. The caller is responsible for fetching existing entities and passing the computed add/update/delete lists. Signature is now:
  ```csharp
  Task<int> UpdateDatabaseAsync(List<T> addEntities, List<T> updateEntities, List<T> deleteEntities, bool hardDelete = false)
  ```
- **`IFileImportServices.ProcessFileAsync` (master overloads)** — added `int rowsToSkip = 0` and `bool fixUnescapedQuotes = false` as trailing optional parameters on both the file-system and blob/stream overloads.
- **`IFileReaderServices.ReadFromFile` (main overloads)** — added `int rowsToSkip = 0` and `bool fixUnescapedQuotes = false` as trailing optional parameters on both the file-system and stream overloads.
- **DI extension constraints** — `AddFileImportServices<T,U,M,F>` now constrains `F` to `class, IFileImportServices<T,U,M>` instead of the concrete `FileImportServices<T,U,M>`, allowing decorated or alternative implementations.

### New features

- **`rowsToSkip`** — skip N leading rows before parsing begins (e.g. metadata rows above the CSV header).
- **`fixUnescapedQuotes`** — instruct the parser to attempt repair of unescaped quote characters in CSV fields.
- Two dedicated convenience overloads on `IFileImportServices` for the new scenarios:
  ```csharp
  ProcessFileAsync(basePath, fileNamePattern, rowsToSkip)
  ProcessFileAsync(basePath, fileNamePattern, rowsToSkip, fixUnescapedQuotes)
  ```

### Bug fixes

- `archiveIfSuccess` was silently dropped in the `(string, string, bool failIfNotFound, bool firstLineContainsEncoding, bool archiveIfSuccess)` overload and always defaulted to `true`. It is now forwarded correctly.
- `GetAllEntitiesAsync()` was called inside the per-file loop, causing N full-table reads when processing multiple files. It is now called once before the loop.

### Internal improvements

- Removed dead `using static System.Runtime.InteropServices.JavaScript.JSType` and `using System.Runtime.CompilerServices` from `FileImportServices`.
- `_logger.LogError(ex, ex.Message)` replaced throughout with descriptive constant strings to prevent structured-logging format exceptions when exception messages contain `{` or `}`.
- Timestamp format changed from `ffff` (ten-thousandths of a second) to `fff` (milliseconds).
- Added `.gitignore` to exclude `bin/`, `obj/`, `.vs/`, `.idea/`, and NuGet artifacts.
- Removed duplicate `LICENSE.txt`; `LICENSE` is the canonical file.
- Added `<GenerateDocumentationFile>true</GenerateDocumentationFile>` to the project, producing an XML doc file alongside the assembly.

---

## [1.0.0] - 2026-05-14

Renamed the library from `GenericFileImportServices` to `GenericFileServices` and added a complete file export pipeline alongside the existing import functionality.

### New features

- **`IFileExportServices<T,C>`** — orchestrates database-to-file exports: data retrieval via a consumer-supplied async delegate, CsvHelper serialisation, and writing to a local path or Azure Blob Storage. Both `ExportToFileAsync` and `ExportToBlobAsync` return `Task<List<string>>`; an empty list indicates success.
- **`IFileWriterServices`** — low-level writer used by `IFileExportServices`. Exposes `WriteToFile`, `WriteToBlobAsync`, `ArchiveExistingFile`, and `ArchiveExistingBlobAsync` directly for consumers that manage data retrieval themselves.
- **`IBlobClientFactory` / `BlobClientFactory`** — abstracts `BlobContainerClient` construction to allow test doubles without a live Azure Storage account.
- **`AddFileExportServices<T,C>`** DI extension — registers `IBlobClientFactory` (singleton), `IFileWriterServices`, and `IFileExportServices<T,C>` with an optional `DbContextOptionsBuilder` overload.
- **Archive support** — both `ExportToFileAsync` and `ExportToBlobAsync` accept `archiveExistingFile`/`archiveExistingBlob` and `archivePath` parameters. Existing files are timestamped and moved before the new output is written.
- **`writeHeader`** — when `false`, suppresses the CSV column header row; output contains data rows only.
- **`writeEncodingHeader`** — when `true`, writes the encoding name as the first line of the output (e.g. `utf-8`). Overridable via `encodingHeaderOverride`.
- **`metadataHeader`** — optional `IReadOnlyDictionary<int, string>` written before the encoding line and CSV header. Entries are output in ascending key order; gaps are ignored.
- **`CsvConfiguration` overloads** — alternative overloads of `WriteToFile`, `WriteToBlobAsync`, `ExportToFileAsync`, and `ExportToBlobAsync` that accept a `CsvConfiguration` directly, giving consumers full CsvHelper control (e.g. `ShouldQuote`, `ClassMap` registration) without mixing concerns with the convenience parameters.

---

## Pre-1.0 — GenericFileImportServices

- Import-only library under the name `GenericFileImportServices`.
- Abstract base `FileImportServices<T,U,C>` with file-system and Azure Blob Storage overloads.
- `DatabaseServices<T,C>` with soft-delete and hard-delete support via `BaseObject`.
- `FileReaderServices<U>` delegating to `MagellanFileServices`.
- DI extension `AddFileImportServices` with optional `DbContextOptionsBuilder` configuration.
