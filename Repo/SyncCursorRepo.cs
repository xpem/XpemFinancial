using Microsoft.EntityFrameworkCore;
using Model.DTO;

namespace Repo
{
    public interface ISyncCursorRepo
    {
        /// <summary>
        /// Returns the server timestamp anchor for the given entity, or DateTime.MinValue
        /// when no pull has been completed yet (triggers a full pull).
        /// </summary>
        Task<DateTime> GetAsync(string entityName);

        /// <summary>
        /// Persists the highest server-side UpdatedAt seen in the last pull response.
        /// Creates the row if it does not exist yet.
        /// </summary>
        Task SaveAsync(string entityName, DateTime serverTimestamp);
    }

    public class SyncCursorRepo(IDbContextFactory<DbCtx> dbCtx) : ISyncCursorRepo
    {
        public async Task<DateTime> GetAsync(string entityName)
        {
            using var db = await dbCtx.CreateDbContextAsync();
            var cursor = await db.SyncCursor.FirstOrDefaultAsync(c => c.EntityName == entityName);
            return cursor?.ServerTimestamp ?? DateTime.MinValue;
        }

        public async Task SaveAsync(string entityName, DateTime serverTimestamp)
        {
            using var db = await dbCtx.CreateDbContextAsync();
            var cursor = await db.SyncCursor.FirstOrDefaultAsync(c => c.EntityName == entityName);

            if (cursor is null)
            {
                db.SyncCursor.Add(new SyncCursorDTO
                {
                    EntityName = entityName,
                    ServerTimestamp = serverTimestamp,
                });
            }
            else
            {
                cursor.ServerTimestamp = serverTimestamp;
                db.SyncCursor.Update(cursor);
            }

            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Stable entity name constants — keeps magic strings in one place.
    /// </summary>
    public static class SyncCursorKeys
    {
        public const string Transaction   = "Transaction";
        public const string RecurringRule = "RecurringRule";
        public const string Category      = "Category";
        public const string Account       = "Account";
    }
}
