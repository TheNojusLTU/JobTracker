namespace JobTracker.Services;

public interface IImportConcurrencyGuard
{
    Task<IImportLease?> TryAcquireAsync(string userId, CancellationToken cancellationToken);
}

public interface IImportLease : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
