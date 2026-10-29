namespace JobTracker.Services;

public interface IApplicationImportService
{
    Task<ApplicationImportResult> ImportAsync(string userId, Stream csv, CancellationToken cancellationToken = default);
}
