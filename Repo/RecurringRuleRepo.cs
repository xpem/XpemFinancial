using Microsoft.EntityFrameworkCore;
using Model.DTO;

namespace Repo
{
    public interface IRecurringRuleRepo
    {
        Task AddAsync(RecurringRuleDTO rule);
        Task UpdateAsync(RecurringRuleDTO rule);
        Task<RecurringRuleDTO?> GetByIdAsync(Guid id);
        Task<RecurringRuleDTO?> GetByExternalIdAsync(int externalId);
        Task<DateTime> GetMaxUpdatedAtAsync();
        Task<IEnumerable<RecurringRuleDTO>> GetAllActiveAsync();
        Task UpsertAsync(RecurringRuleDTO rule);
    }

    public class RecurringRuleRepo(IDbContextFactory<DbCtx> DbCtx) : IRecurringRuleRepo
    {
        public async Task AddAsync(RecurringRuleDTO rule)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            db.RecurringRule.Add(rule);
            await db.SaveChangesAsync();
        }

        public async Task UpdateAsync(RecurringRuleDTO rule)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            db.RecurringRule.Update(rule);
            await db.SaveChangesAsync();
        }

        public async Task<RecurringRuleDTO?> GetByIdAsync(Guid id)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.RecurringRule.FirstOrDefaultAsync(r => r.RecurringRuleId == id);
        }

        public async Task<RecurringRuleDTO?> GetByExternalIdAsync(int externalId)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.RecurringRule.FirstOrDefaultAsync(r => r.ExternalId == externalId);
        }

        public async Task<DateTime> GetMaxUpdatedAtAsync()
        {
            using var db = await DbCtx.CreateDbContextAsync();
            if (!await db.RecurringRule.AnyAsync()) return DateTime.MinValue;
            return await db.RecurringRule.MaxAsync(r => r.UpdatedAt);
        }

        public async Task<IEnumerable<RecurringRuleDTO>> GetAllActiveAsync()
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.RecurringRule.Where(r => r.Inactive == false).ToListAsync();
        }

        public async Task UpsertAsync(RecurringRuleDTO rule)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            var existing = await db.RecurringRule.FirstOrDefaultAsync(r => r.RecurringRuleId == rule.RecurringRuleId);
            if (existing is null)
            {
                db.RecurringRule.Add(rule);
            }
            else if (rule.UpdatedAt > existing.UpdatedAt)
            {
                existing.Description = rule.Description;
                existing.Amount = rule.Amount;
                existing.Type = rule.Type;
                existing.CategoryId = rule.CategoryId;
                existing.AccountId = rule.AccountId;
                existing.Frequency = rule.Frequency;
                existing.StartDate = rule.StartDate;
                existing.EndDate = rule.EndDate;
                existing.Inactive = rule.Inactive;
                existing.ExternalId = rule.ExternalId;
                existing.UpdatedAt = rule.UpdatedAt;
                db.RecurringRule.Update(existing);
            }
            await db.SaveChangesAsync();
        }
    }
}
