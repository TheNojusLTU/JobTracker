using JobTracker.Services;

namespace JobTracker.Tests.Integration;

public sealed class InMemoryImportConcurrencyGuard : IImportConcurrencyGuard
{
    private readonly HashSet<string> activeUsers = [];

    public Task<IImportLease?> TryAcquireAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (activeUsers)
        {
            return Task.FromResult<IImportLease?>(
                activeUsers.Add(userId) ? new Lease(activeUsers, userId) : null);
        }
    }

    private sealed class Lease(HashSet<string> activeUsers, string userId) : IImportLease
    {
        public Task CommitAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            lock (activeUsers)
            {
                activeUsers.Remove(userId);
            }

            return ValueTask.CompletedTask;
        }
    }
}
