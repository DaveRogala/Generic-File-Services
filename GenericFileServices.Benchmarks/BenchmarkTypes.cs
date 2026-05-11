namespace GenericFileServices.Benchmarks;

// ── Domain types ─────────────────────────────────────────────────────────────

public class BenchEntity : BaseObject
{
    [SetsRequiredMembers]
    public BenchEntity() { Name = null!; }

    [SetsRequiredMembers]
    public BenchEntity(string name) { Name = name; }

    public required string Name { get; set; }
}

public class BenchDto
{
    public string Name { get; set; } = "";
}

public class BenchDbContext(DbContextOptions<BenchDbContext> options) : DbContext(options);

// ── Concrete FileImportServices for reconciliation benchmarks ─────────────────

public class BenchFileImportServices(
    IDatabaseServices<BenchEntity, BenchDbContext> dbServices,
    IFileReaderServices<BenchDto> readerServices,
    ILogger<BenchFileImportServices> logger)
    : FileImportServices<BenchEntity, BenchDto, BenchDbContext>(dbServices, readerServices, logger)
{
    public override List<BenchEntity> GetAddEntities(List<BenchEntity> existing, List<BenchDto> dtos) =>
        dtos.Where(d => existing.All(e => e.Name != d.Name))
            .Select(d => new BenchEntity(d.Name))
            .ToList();

    public override List<BenchEntity> GetUpdateEntities(List<BenchEntity> existing, List<BenchDto> dtos) => [];

    public override List<BenchEntity> GetDeleteEntities(List<BenchEntity> existing, List<BenchDto> dtos) =>
        existing.Where(e => e.DateDeletedUtc is null && dtos.All(d => d.Name != e.Name))
                .ToList();
}

// ── No-op repository for UpdateDatabase benchmarks ────────────────────────────

internal sealed class FakeRepository : IGenericRepository<BenchEntity, BenchDbContext, int>
{
    public Task<BenchEntity> AddAsync(BenchEntity entity, CancellationToken ct = default) =>
        Task.FromResult(entity);

    public BenchEntity Update(BenchEntity entity) => entity;

    public BenchEntity Delete(BenchEntity entity) => entity;

    public Task<BenchEntity?> GetAsync(int id, CancellationToken ct = default) =>
        Task.FromResult<BenchEntity?>(null);

    public Task<IEnumerable<BenchEntity>> AllAsync(
        QueryTrackingBehavior tracking = QueryTrackingBehavior.TrackAll,
        CancellationToken ct = default) =>
        Task.FromResult(Enumerable.Empty<BenchEntity>());

    public Task<IEnumerable<BenchEntity>> AllAsync(
        int skip, int take,
        Func<IQueryable<BenchEntity>, IOrderedQueryable<BenchEntity>> orderBy,
        QueryTrackingBehavior tracking = QueryTrackingBehavior.TrackAll,
        CancellationToken ct = default) =>
        Task.FromResult(Enumerable.Empty<BenchEntity>());

    public Task<IEnumerable<BenchEntity>> FindAsync(
        Expression<Func<BenchEntity, bool>> predicate,
        QueryTrackingBehavior tracking = QueryTrackingBehavior.TrackAll,
        CancellationToken ct = default) =>
        Task.FromResult(Enumerable.Empty<BenchEntity>());

    public Task<BenchEntity?> FindFirstAsync(
        Expression<Func<BenchEntity, bool>> predicate,
        Func<IQueryable<BenchEntity>, IOrderedQueryable<BenchEntity>> orderBy,
        QueryTrackingBehavior tracking = QueryTrackingBehavior.TrackAll,
        CancellationToken ct = default) =>
        Task.FromResult<BenchEntity?>(null);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);

    public void Dispose() { }
}

// ── Zero-overhead IFileServices stub for FileRead benchmarks ──────────────────

internal sealed class StubFileServices : IFileServices
{
    private readonly ObjectResult<BenchDto> _result;

    public StubFileServices(List<BenchDto> data) =>
        _result = new ObjectResult<BenchDto>(data, []);

    // Cast is safe: callers in benchmarks always use T = BenchDto.
    private ObjectResult<T> Result<T>() => (ObjectResult<T>)(object)_result;

    public ObjectResult<T> GetDataFromFile<T>(string filePath, Encoding encoding, bool skipEncodingHeader, string delimiter = ",") => Result<T>();
    public ObjectResult<T> GetDataFromFile<T>(string filePath, Encoding encoding, int rowsToSkip, string delimiter = ",", bool fixUnescapedQuotes = false) => Result<T>();
    public ObjectResult<T> GetDataFromFile<T>(string filePath, string delimiter = ",") => Result<T>();
    public ObjectResult<T> GetDataFromFile<T>(Stream stream, Encoding encoding, bool skipEncodingHeader, string delimiter = ",") => Result<T>();
    public ObjectResult<T> GetDataFromFile<T>(Stream stream, Encoding encoding, int rowsToSkip, string delimiter = ",", bool fixUnescapedQuotes = false) => Result<T>();
    public ObjectResult<T> GetDataFromFile<T>(Stream stream, string delimiter = ",") => Result<T>();

    public void HandleFileError(string basePath, string fileName, string exceptionMessage, string timeStamp, List<string>? errors = null) { }
    public void HandleFileSuccess(string basePath, string fileName, string timeStamp) { }
    public Task HandleFileErrorAsync(BlobContainerClient containerClient, string filePath, string exceptionMessage, string timeStamp, List<string>? errors = null) => Task.CompletedTask;
    public Task HandleFileSuccessAsync(BlobContainerClient containerClient, string filePath, string timeStamp) => Task.CompletedTask;
    public void WriteDataToFile<T>(string filePath, List<T> data) { }
    public void WriteDataToFile<T>(string filePath, List<T> data, bool printEncoding) { }
    public void WriteDataToFile<T>(string filePath, List<T> data, string delimiter = ",", bool useHeaders = true, bool printEncoding = false) { }
    public void WriteDataToFile<T>(string filePath, List<T> data, Encoding encoding, string delimiter = ",", bool useHeaders = true, bool printEncoding = false) { }
}
