using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace api.Data;

public static class DbContextDryRunExtensions
{
    /// Runs work inside a transaction. Rolls back if DryRun=true.
    public static async Task<T> WithDryRunAsync<T>(
        this AppDbContext db,
        bool dryRun,
        Func<Task<T>> work)
    {
        await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync();
        T result = await work();

        if (dryRun)
        {
            await tx.RollbackAsync();
            foreach (var entry in db.ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
        else
        {
            await tx.CommitAsync();
        }
        return result;
    }
}
