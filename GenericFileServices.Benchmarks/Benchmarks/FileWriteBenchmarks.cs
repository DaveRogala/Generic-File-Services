using BenchmarkDotNet.Attributes;

namespace GenericFileServices.Benchmarks.Benchmarks;

/// <summary>
/// Measures FileWriterServices.WriteToFile across the combinations of optional
/// header features (metadata, encoding line, column header suppression) and the
/// CsvConfiguration overload.
///
/// Baseline: default convenience overload with no extra headers.
/// All header-flag benchmarks isolate a single flag change over the baseline.
/// CsvConfig benchmarks compare passing a plain CsvConfiguration against using
/// ShouldQuote to force quoting on every string field.
///
/// BenchExportDto has four columns (string, int, decimal, DateTime) to give
/// CsvHelper a representative workload.
/// </summary>
[MemoryDiagnoser]
public class FileWriteBenchmarks
{
    [Params(100, 1_000, 10_000)]
    public int N { get; set; }

    private FileWriterServices _sut = null!;
    private string _tempDir = null!;
    private List<BenchExportDto> _records = null!;
    private Encoding _encoding = null!;
    private IReadOnlyDictionary<int, string> _metadata = null!;
    private CsvConfiguration _csvConfig = null!;
    private CsvConfiguration _csvConfigShouldQuote = null!;

    [GlobalSetup]
    public void Setup()
    {
        _sut = new FileWriterServices(
            NullLogger<FileWriterServices>.Instance,
            null!);

        _tempDir = Path.Combine(Path.GetTempPath(), $"gfs_bench_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        _records = Enumerable.Range(0, N)
            .Select(i => new BenchExportDto
            {
                Name  = $"Item{i}",
                Value = i,
                Price = i * 1.5m,
                Date  = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            })
            .ToList();

        _metadata = new Dictionary<int, string>
        {
            { 1, "Source: Benchmark" },
            { 2, "ExtractDate: 2026-01-01" },
            { 3, "Version: 1" }
        };

        _csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture);

        _csvConfigShouldQuote = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            ShouldQuote = args => args.FieldType == typeof(string)
        };
    }

    [GlobalCleanup]
    public void Cleanup() => Directory.Delete(_tempDir, recursive: true);

    // ── Convenience overload ─────────────────────────────────────────────────

    /// <summary>Default write: column header + data rows, no extras.</summary>
    [Benchmark(Baseline = true)]
    public void WriteToFile_Default() =>
        _sut.WriteToFile(_tempDir, "out.csv", _records, _encoding);

    /// <summary>Adds a single encoding line before the column header.</summary>
    [Benchmark]
    public void WriteToFile_EncodingHeader() =>
        _sut.WriteToFile(_tempDir, "out.csv", _records, _encoding,
            writeEncodingHeader: true);

    /// <summary>Prepends three metadata lines before encoding and column header.</summary>
    [Benchmark]
    public void WriteToFile_MetadataHeader() =>
        _sut.WriteToFile(_tempDir, "out.csv", _records, _encoding,
            metadataHeader: _metadata);

    /// <summary>Suppresses the column header row; data rows only.</summary>
    [Benchmark]
    public void WriteToFile_NoColumnHeader() =>
        _sut.WriteToFile(_tempDir, "out.csv", _records, _encoding,
            writeHeader: false);

    /// <summary>All optional headers active simultaneously.</summary>
    [Benchmark]
    public void WriteToFile_AllHeaders() =>
        _sut.WriteToFile(_tempDir, "out.csv", _records, _encoding,
            metadataHeader: _metadata,
            writeEncodingHeader: true);

    // ── CsvConfiguration overload ────────────────────────────────────────────

    /// <summary>CsvConfiguration overload with default settings; baseline comparison for overload overhead.</summary>
    [Benchmark]
    public void WriteToFile_CsvConfig() =>
        _sut.WriteToFile(_tempDir, "out.csv", _records, _encoding, _csvConfig);

    /// <summary>CsvConfiguration overload with ShouldQuote evaluated per field per row.</summary>
    [Benchmark]
    public void WriteToFile_CsvConfig_ShouldQuote() =>
        _sut.WriteToFile(_tempDir, "out.csv", _records, _encoding, _csvConfigShouldQuote);
}
