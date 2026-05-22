namespace GenericFileServices.Models;

/// <summary>
/// Configuration options for a file import operation via <see cref="GenericFileServices.Contracts.IFileImportServices{T,U,C}"/>.
/// Pass an instance to the canonical <c>ProcessFileAsync</c> overloads to avoid
/// managing the large set of optional boolean parameters individually.
/// </summary>
public record FileImportOptions
{
    /// <summary>
    /// Column delimiter. Defaults to <c>","</c>.
    /// </summary>
    public string Delimiter { get; init; } = ",";

    /// <summary>
    /// Character encoding of the file. Defaults to <see cref="System.Text.Encoding.Default"/>.
    /// </summary>
    public System.Text.Encoding Encoding { get; init; } = System.Text.Encoding.Default;

    /// <summary>
    /// When <c>true</c>, the first data line is treated as an encoding header row and skipped.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool FirstLineContainsEncoding { get; init; } = false;

    /// <summary>
    /// When <c>true</c>, throw if no files match the pattern. Defaults to <c>true</c>.
    /// </summary>
    public bool FailIfNotFound { get; init; } = true;

    /// <summary>
    /// When <c>true</c>, allow more than one file matching the pattern. Defaults to <c>false</c>.
    /// </summary>
    public bool MultipleFiles { get; init; } = false;

    /// <summary>
    /// When <c>true</c> (the default), move the file to the archive location after a clean import.
    /// </summary>
    public bool ArchiveIfSuccess { get; init; } = true;

    /// <summary>
    /// When <c>true</c>, permanently delete matched entities; when <c>false</c> (the default),
    /// sets <c>DateDeletedUtc</c> (soft delete).
    /// </summary>
    public bool HardDelete { get; init; } = false;

    /// <summary>
    /// Number of leading rows to skip before parsing begins. Defaults to <c>0</c>.
    /// </summary>
    public int RowsToSkip { get; init; } = 0;

    /// <summary>
    /// When <c>true</c>, attempt to repair unescaped quote characters in CSV fields.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool FixUnescapedQuotes { get; init; } = false;

    /// <summary>
    /// When <c>true</c> (the default), the file contains a header row that CsvHelper uses for
    /// column mapping. When <c>false</c>, the file is headerless and column order is
    /// determined by <c>[Index]</c> attributes on the DTO type.
    /// </summary>
    public bool FileHasHeader { get; init; } = true;

    /// <summary>
    /// When <c>true</c>, throw if the parsed file contains zero data records.
    /// Use for source-of-truth files that must never be empty.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool FailIfNoRecords { get; init; } = false;

    /// <summary>
    /// When set (0–100), abort the import and skip all database updates if the number of
    /// adds <em>or</em> deletes exceeds this percentage of the current active record count.
    /// <c>null</c> disables the check (default).
    /// </summary>
    public double? ErrorThresholdPercentage { get; init; } = null;

    /// <summary>
    /// When set (0–100), log a warning if the number of adds <em>or</em> deletes exceeds this
    /// percentage of the current active record count. The import still proceeds.
    /// <c>null</c> disables the check (default).
    /// </summary>
    public double? WarningThresholdPercentage { get; init; } = null;

    /// <summary>A <see cref="FileImportOptions"/> instance with all default values.</summary>
    public static readonly FileImportOptions Default = new();
}
