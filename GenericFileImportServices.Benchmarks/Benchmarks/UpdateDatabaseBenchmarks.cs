using BenchmarkDotNet.Attributes;

namespace GenericFileImportServices.Benchmarks.Benchmarks;

/// <summary>
/// Measures the timestamp-stamping loops inside DatabaseServices.UpdateDatabaseAsync.
/// Uses a no-op FakeRepository so the benchmark captures only the service loop overhead,
/// not database round-trip time.
///
/// Covers all four operation types to verify uniform O(n) scaling across them.
/// </summary>
[MemoryDiagnoser]
public class UpdateDatabaseBenchmarks
{
    [Params(100, 1_000)]
    public int N { get; set; }

    private DatabaseServices<BenchEntity, BenchDbContext> _sut = null!;
    private List<BenchEntity> _entities = null!;
    private List<BenchEntity> _adds = null!;
    private List<BenchEntity> _updates = null!;
    private List<BenchEntity> _deletes = null!;
    private static readonly List<BenchEntity> Empty = [];

    [GlobalSetup]
    public void Setup()
    {
        _sut = new DatabaseServices<BenchEntity, BenchDbContext>(
            new FakeRepository(),
            NullLogger<DatabaseServices<BenchEntity, BenchDbContext>>.Instance);

        _entities = Enumerable.Range(0, N)
            .Select(i => new BenchEntity($"Entity{i}"))
            .ToList();

        int third = N / 3;
        _adds    = _entities.Take(third).ToList();
        _updates = _entities.Skip(third).Take(third).ToList();
        _deletes = _entities.Skip(third * 2).ToList();
    }

    [Benchmark]
    public Task<int> AllAdds() => _sut.UpdateDatabaseAsync(_entities, Empty, Empty);

    [Benchmark]
    public Task<int> AllUpdates() => _sut.UpdateDatabaseAsync(Empty, _entities, Empty);

    [Benchmark]
    public Task<int> AllSoftDeletes() => _sut.UpdateDatabaseAsync(Empty, Empty, _entities, hardDelete: false);

    [Benchmark]
    public Task<int> AllHardDeletes() => _sut.UpdateDatabaseAsync(Empty, Empty, _entities, hardDelete: true);

    [Benchmark]
    public Task<int> Mixed() => _sut.UpdateDatabaseAsync(_adds, _updates, _deletes);
}
