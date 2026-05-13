# Changelog

All notable changes to GenericFileImportServices are documented here.

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

## [1.0.0] - initial release

- Initial release of `GenericFileImportServices`.
- Abstract base `FileImportServices<T,U,C>` with file-system and Azure Blob Storage overloads.
- `DatabaseServices<T,C>` with soft-delete and hard-delete support via `BaseObject`.
- `FileReaderServices<U>` delegating to `MagellanFileServices`.
- DI extension `AddFileImportServices` with optional `DbContextOptionsBuilder` configuration.
