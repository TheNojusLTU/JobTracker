using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using JobTracker.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace JobTracker.Services;

public sealed class PostgresImportConcurrencyGuard(ApplicationDbContext dbContext) : IImportConcurrencyGuard
{
    public async Task<IImportLease?> TryAcquireAsync(string userId, CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var acquired = false;
        try
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"JobTracker.CsvImport:{userId}"));
            var key = BinaryPrimitives.ReadInt64BigEndian(hash);
            acquired = await dbContext.Database
                .SqlQueryRaw<bool>("SELECT pg_try_advisory_xact_lock({0}) AS \"Value\"", key)
                .SingleAsync(cancellationToken);
            return acquired ? new Lease(transaction) : null;
        }
        finally
        {
            if (!acquired)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private sealed class Lease(IDbContextTransaction transaction) : IImportLease
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
