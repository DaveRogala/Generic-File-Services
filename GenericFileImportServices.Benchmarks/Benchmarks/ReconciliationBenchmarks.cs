using BenchmarkDotNet.Attributes;

namespace GenericFileImportServices.Benchmarks.Benchmarks;

/// <summary>
/// Measures the three reconciliation methods (add/update/delete entity detection)
/// that consumers implement on FileImportServices.
///
/// The default implementation uses O(n²) linear scans, so these benchmarks surface
/// how that scales and whether a HashSet-based approach would be worthwhile.
///
/// Setup: N existing entities, N DTOs with 50% overlap.
/// Expected: ~N/2 adds, 0 updates, ~N/2 deletes.
/// </summary>
[MemoryDiagnoser]
public class ReconciliationBenchmarks
{
    [Params(100, 1_000, 10_000)]
    public int N { get; set; }

    private List<BenchEntity> _existing = null!;
    private List<BenchDto> _dtos = null!;
    private BenchFileImportServices _sut = null!;

    [GlobalSetup]
    public void Setup()
    {
        // First N/2 entities have no matching DTO → will be deleted.
        // Last N/2 entities match DTOs → no change.
        // N new DTOs starting at N/2 → will be added.
        _existing = Enumerable.Range(0, N)
            .Select(i => new BenchEntity($"Item{i}"))
            .ToList();

        _dtos = Enumerable.Range(N / 2, N)
            .Select(i => new BenchDto { Name = $"Item{i}" })
            .ToList();

        _sut = new BenchFileImportServices(null!, null!, NullLogger<BenchFileImportServices>.Instance);
    }

    [Benchmark]
    public List<BenchEntity> GetAddEntities() => _sut.GetAddEntities(_existing, _dtos);

    [Benchmark]
    public List<BenchEntity> GetUpdateEntities() => _sut.GetUpdateEntities(_existing, _dtos);

    [Benchmark]
    public List<BenchEntity> GetDeleteEntities() => _sut.GetDeleteEntities(_existing, _dtos);
}
