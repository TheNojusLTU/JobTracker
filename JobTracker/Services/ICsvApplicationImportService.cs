namespace JobTracker.Services;

public interface ICsvApplicationImportService
{
    const long MaxImportBytes = 1_048_576;

    Task<CsvApplicationImportResult> ImportAsync(
        Stream csvStream, string userId, CancellationToken cancellationToken);
}
