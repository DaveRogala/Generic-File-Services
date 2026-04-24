using BenchmarkDotNet.Attributes;

namespace GenericFileImportServices.Benchmarks.Benchmarks;

/// <summary>
/// Measures the routing + wrapping overhead in FileReaderServices.ReadFromFile (stream overload).
/// A StubFileServices returns pre-built results instantly, so the benchmark captures only:
///   - the conditional overload selection (bool vs int path)
///   - FileResults allocation and list construction
///
/// Vary N to confirm the wrapping cost is O(1) regardless of result size
/// (the list itself is not copied, only referenced).
/// </summary>
[MemoryDiagnoser]
public class FileReadBenchmarks
{
    [Params(10, 100, 1_000)]
    public int N { get; set; }

    private FileReaderServices<BenchDto> _sut = null!;
    private MemoryStream _stream = null!;

    [GlobalSetup]
    public void Setup()
    {
        var data = Enumerable.Range(0, N)
            .Select(i => new BenchDto { Name = $"Row{i}" })
            .ToList();

        _sut = new FileReaderServices<BenchDto>(
            new StubFileServices(data),
            NullLogger<FileReaderServices<BenchDto>>.Instance);

        _stream = new MemoryStream();
    }

    [GlobalCleanup]
    public void Cleanup() => _stream.Dispose();

    /// <summary>Routes to the bool overload of GetDataFromFile (no rowsToSkip).</summary>
    [Benchmark(Baseline = true)]
    public List<FileResults<BenchDto>> BoolOverload() =>
        _sut.ReadFromFile(_stream, "bench.csv", Encoding.UTF8, firstLineContainsEncoding: false);

    /// <summary>Routes to the int overload of GetDataFromFile (rowsToSkip = 1).</summary>
    [Benchmark]
    public List<FileResults<BenchDto>> IntOverload() =>
        _sut.ReadFromFile(_stream, "bench.csv", Encoding.UTF8, firstLineContainsEncoding: false, rowsToSkip: 1);

    /// <summary>Routes to the int overload via fixUnescapedQuotes flag.</summary>
    [Benchmark]
    public List<FileResults<BenchDto>> FixQuotesOverload() =>
        _sut.ReadFromFile(_stream, "bench.csv", Encoding.UTF8, firstLineContainsEncoding: false, fixUnescapedQuotes: true);
}
