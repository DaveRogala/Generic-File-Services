namespace GenericFileServices;

/// <summary>
/// Shared compile-time constants used across GenericFileServices services.
/// </summary>
internal static class Constants
{
    /// <summary>
    /// Timestamp format string with 100-nanosecond tick resolution, used when
    /// constructing archive file names and blob paths.
    /// Example output: <c>20260101120000000000</c> (yyyyMMddHHmmssfffffff).
    /// </summary>
    internal const string TimestampFormat = "yyyyMMddHHmmssfffffff";
}
