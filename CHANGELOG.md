# Changelog

All notable changes to GenericFileImportServices are documented here.

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
