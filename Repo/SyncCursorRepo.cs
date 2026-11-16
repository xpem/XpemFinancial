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

        /// <summary>Removes a row (no-op when it does not exist).</summary>
        Task DeleteAsync(string entityName);

        /// <summary>Lists the names of the rows that start with <paramref name="prefix"/>.</summary>
        Task<List<string>> ListNamesAsync(string prefix);
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

        public async Task DeleteAsync(string entityName)
        {
            using var db = await dbCtx.CreateDbContextAsync();
            var cursor = await db.SyncCursor.FirstOrDefaultAsync(c => c.EntityName == entityName);
            if (cursor is null) return;

            db.SyncCursor.Remove(cursor);
            await db.SaveChangesAsync();
        }

        public async Task<List<string>> ListNamesAsync(string prefix)
        {
            using var db = await dbCtx.CreateDbContextAsync();
            return await db.SyncCursor
                .Where(c => c.EntityName.StartsWith(prefix))
                .Select(c => c.EntityName)
                .ToListAsync();
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

        // Outbox markers: a row means "this record was edited locally and still has to be pushed".
        // ServerTimestamp holds the record's local UpdatedAt at the time of the edit, so a marker is only
        // cleared when the pushed state is still the latest one. Kept here to avoid a DB schema change.
        // (EntityName is limited to 50 chars: prefix + Guid("N") = 32 chars.)
        public const string RuleOutboxPrefix     = "RuleOutbox:";
        public const string CategoryOutboxPrefix = "CatOutbox:";

        public static string RuleOutbox(Guid recurringRuleId) => RuleOutboxPrefix + recurringRuleId.ToString("N");
        public static string CategoryOutbox(Guid categoryId)  => CategoryOutboxPrefix + categoryId.ToString("N");

        public static Guid? ParseGuid(string outboxName, string prefix)
            => Guid.TryParseExact(outboxName[prefix.Length..], "N", out var id) ? id : null;
    }
}
