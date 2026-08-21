using ApiRepo;
using Model.DTO;
using Model.Req;
using Model.Resp;
using Repo;
using Service.Transaction;
namespace Service.Recurring
{
    public interface IRecurringRuleService
    {
        Task<ServiceResp> SaveAsync(RecurringRuleDTO rule, bool isOnline);
        Task<ServiceResp> EditOccurrenceAsync(EditOccurrenceReq req, bool isOnline);
        Task<ServiceResp> CancelAsync(CancelRuleReq req, bool isOnline);
        Task RunSchedulerAsync(DateTime? horizon = null);
        Task<IEnumerable<RecurringRuleDTO>> GetAllActiveAsync();
        Task<DateTime> GetLastUpdatedAtAsync();
        Task PullAsync(int uid, DateTime lastUpdatedAt);
    }

    public class RecurringRuleService(
        Repo.IRecurringRuleRepo recurringRuleRepo,
        IRecurringScheduler recurringScheduler,
        IRecurringRuleApiRepo recurringRuleApiRepo,
        ITransactionService transactionService,
        ICategoryRepo categoryRepo,
        IAccountRepo accountRepo,
        ISyncCursorRepo syncCursorRepo) : IRecurringRuleService
    {
        public async Task<ServiceResp> SaveAsync(RecurringRuleDTO rule, bool isOnline)
        {
            if (rule.Amount == 0)
                return new ServiceResp(false, "Amount must be different from zero.");

            if (string.IsNullOrWhiteSpace(rule.Description) && rule.CategoryId == 0)
                return new ServiceResp(false, "A description or category is required.");

            if (rule.EndDate != null && rule.EndDate < rule.StartDate)
                return new ServiceResp(false, "End date must be on or after the start date.");

            if (!Enum.IsDefined(typeof(Frequency), rule.Frequency))
                return new ServiceResp(false, "Invalid frequency value.");

            // Assign a new GUID if not already set
            if (rule.RecurringRuleId == Guid.Empty)
                rule.RecurringRuleId = Guid.NewGuid();

            rule.CreatedAt = DateTime.Now;
            rule.UpdatedAt = DateTime.Now;

            await recurringRuleRepo.AddAsync(rule);
            await recurringScheduler.GeneratePendingAsync([rule]);

            if (isOnline)
            {
                RecurringRuleReq req = new()
                {
                    RecurringRuleId = rule.RecurringRuleId,
                    Description = rule.Description,
                    Amount = rule.Amount,
                    Type = (int)rule.Type,
                    CategoryId = rule.CategoryExternalId,
                    AccountId = rule.AccountId,
                    Frequency = (int)rule.Frequency,
                    StartDate = rule.StartDate,
                    EndDate = rule.EndDate,
                    Inactive = rule.Inactive,
                    UpdatedAt = rule.UpdatedAt,
                };

                int serverId = await recurringRuleApiRepo.PostAsync(req);
                rule.ExternalId = serverId;
                await recurringRuleRepo.UpdateAsync(rule);
            }

            return new ServiceResp(true);
        }

        public async Task<ServiceResp> EditOccurrenceAsync(EditOccurrenceReq req, bool isOnline)
        {
            try
            {
                switch (req.Scope)
                {
                    case EditScope.ThisOnly:
                        {
                            // Mark the occurrence as a customized exception so that:
                            // 1. The scheduler never overwrites it on this device (existingDates covers it).
                            // 2. Other devices pull it from the server and treat it as an override,
                            //    skipping projection generation for that specific date.
                            var occurrence = await transactionService.GetByIdAsync(req.TransactionId);
                            occurrence.Description = req.UpdatedRule.Description ?? occurrence.Description;
                            occurrence.Amount = req.UpdatedRule.Amount;
                            occurrence.CategoryId = req.UpdatedRule.CategoryId;
                            occurrence.CategoryExternalId = req.UpdatedRule.CategoryExternalId;
                            occurrence.Type = req.UpdatedRule.Type;
                            occurrence.IsCustomized = true;
                            occurrence.UpdatedAt = DateTime.Now;
                            // isOnline is passed through so UpdateAsync can POST/PUT to the server.
                            await transactionService.UpdateAsync(occurrence, isOnline);
                            break;
                        }

                    case EditScope.ThisAndFuture:
                        {
                            var rule = await recurringRuleRepo.GetByIdAsync(req.RecurringRuleId)
                                                 ?? throw new InvalidOperationException($"Rule {req.RecurringRuleId} not found.");
                            var targetOccurrence = await transactionService.GetByIdAsync(req.TransactionId);

                            rule.Description = req.UpdatedRule.Description;
                            rule.Amount = req.UpdatedRule.Amount;
                            rule.Type = req.UpdatedRule.Type;
                            rule.CategoryId = req.UpdatedRule.CategoryId;
                            rule.AccountId = req.UpdatedRule.AccountId;
                            rule.Frequency = req.UpdatedRule.Frequency;
                            rule.EndDate = req.UpdatedRule.EndDate;
                            
                            // Update StartDate to the edited occurrence date.
                            // This ensures that when syncing with the server, only occurrences
                            // from this date onwards are regenerated, preserving past occurrences.
                            rule.StartDate = targetOccurrence.Date;
                            rule.UpdatedAt = DateTime.Now;

                            // Update the rule in local DB
                            await recurringRuleRepo.UpdateAsync(rule);

                            // Push rule to server FIRST (if online) to ensure other clients
                            // see the new StartDate before receiving converted transactions
                            if (isOnline)
                                await PushRuleAsync(rule);

                            // Convert past occurrences to standalone transactions.
                            // This preserves them even after sync, since the rule's new StartDate
                            // will no longer cover these dates.
                            // IMPORTANT: This updates transactions and pushes them to the server,
                            // so the rule MUST be updated on the server first to avoid race conditions.
                            await ConvertPastOccurrencesToStandaloneAsync(req.RecurringRuleId, targetOccurrence.Date, isOnline);

                            // Soft-delete future occurrences and regenerate them with new rule values
                            await transactionService.DeleteFutureOccurrencesAsync(req.RecurringRuleId, targetOccurrence.Date);
                            await recurringScheduler.GeneratePendingAsync([rule]);

                            break;
                        }

                    case EditScope.All:
                        {
                            var rule = await recurringRuleRepo.GetByIdAsync(req.RecurringRuleId)
                                       ?? throw new InvalidOperationException($"Rule {req.RecurringRuleId} not found.");

                            rule.Description = req.UpdatedRule.Description;
                            rule.Amount = req.UpdatedRule.Amount;
                            rule.Type = req.UpdatedRule.Type;
                            rule.CategoryId = req.UpdatedRule.CategoryId;
                            rule.AccountId = req.UpdatedRule.AccountId;
                            rule.Frequency = req.UpdatedRule.Frequency;
                            rule.EndDate = req.UpdatedRule.EndDate;
                            rule.UpdatedAt = DateTime.Now;

                            await recurringRuleRepo.UpdateAsync(rule);

                            var occurrences = await transactionService.GetByRecurringRuleIdAsync(req.RecurringRuleId);
                            foreach (var occurrence in occurrences.Where(o => !o.Inactive))
                            {
                                occurrence.Description = req.UpdatedRule.Description ?? occurrence.Description;
                                occurrence.Amount = req.UpdatedRule.Amount;
                                occurrence.Type = req.UpdatedRule.Type;
                                occurrence.CategoryId = req.UpdatedRule.CategoryId;
                                occurrence.UpdatedAt = DateTime.Now;
                                await transactionService.UpdateAsync(occurrence, isOnline: false);
                            }

                            if (isOnline)
                                await PushRuleAsync(rule);

                            break;
                        }
                }

                return new ServiceResp(true);
            }
            catch (Exception ex)
            {
                return new ServiceResp(false, ex.Message);
            }
        }

        public async Task<ServiceResp> CancelAsync(CancelRuleReq req, bool isOnline)
        {
            try
            {
                switch (req.Scope)
                {
                    case CancelScope.FromThisOnwards:
                        {
                            var rule = await recurringRuleRepo.GetByIdAsync(req.RecurringRuleId)
                                                   ?? throw new InvalidOperationException($"Rule {req.RecurringRuleId} not found.");
                            var targetOccurrence = await transactionService.GetByIdAsync(req.TransactionId);

                            rule.EndDate = targetOccurrence.Date.AddDays(-1);
                            rule.UpdatedAt = DateTime.Now;

                            await transactionService.DeleteFutureOccurrencesAsync(req.RecurringRuleId, targetOccurrence.Date);
                            await recurringRuleRepo.UpdateAsync(rule);

                            if (isOnline)
                                await PushRuleAsync(rule);

                            break;
                        }

                    case CancelScope.EntireRule:
                        {
                            var rule = await recurringRuleRepo.GetByIdAsync(req.RecurringRuleId)
                                       ?? throw new InvalidOperationException($"Rule {req.RecurringRuleId} not found.");

                            rule.Inactive = true;
                            rule.UpdatedAt = DateTime.Now;

                            await transactionService.DeleteFutureOccurrencesAsync(req.RecurringRuleId, DateTime.MinValue);
                            await recurringRuleRepo.UpdateAsync(rule);

                            if (isOnline)
                                await PushRuleAsync(rule);

                            break;
                        }
                }

                return new ServiceResp(true);
            }
            catch (Exception ex)
            {
                return new ServiceResp(false, ex.Message);
            }
        }

        public async Task RunSchedulerAsync(DateTime? horizon = null)
        {
            IEnumerable<RecurringRuleDTO> rules = await GetAllActiveAsync();
            await recurringScheduler.GeneratePendingAsync(rules, horizon);
        }

        public async Task<IEnumerable<RecurringRuleDTO>> GetAllActiveAsync()
        {
            return await recurringRuleRepo.GetAllActiveAsync();
        }

        public async Task<DateTime> GetLastUpdatedAtAsync()
            => await syncCursorRepo.GetAsync(SyncCursorKeys.RecurringRule);

        public async Task PullAsync(int uid, DateTime lastUpdatedAt)
        {
            const int pageSize = 100;
            int page = 1;
            DateTime maxServerTs = DateTime.MinValue;
            List<RecurringRuleDTO> upsertedRules = [];

            while (true)
            {
                List<Model.Resp.Api.RecurringRuleApiRes>? results = await recurringRuleApiRepo.GetByUpdatedAtAsync(lastUpdatedAt, page);

                if (results is null || results.Count == 0) break;

                foreach (var res in results)
                {
                    // Resolve local CategoryId from the API's CategoryId (which is the ExternalId)
                    int localCategoryId = 0;
                    if (res.CategoryId.HasValue)
                    {
                        var localCategory = await categoryRepo.GetByExternalIdAsync(res.CategoryId.Value);
                        localCategoryId = localCategory?.Id ?? 0;
                    }

                    // Resolve local AccountId from the API's AccountId (which is the ExternalId).
                    // Without this, the scheduler would write the server's external ID as a FK into
                    // the local Transaction table, causing a FK constraint exception.
                    int? localAccountId = null;
                    if (res.AccountId.HasValue)
                    {
                        var localAccount = await accountRepo.GetByExternalIdAsync(res.AccountId.Value);
                        if (localAccount is not null)
                            localAccountId = localAccount.Id;
                    }

                    RecurringRuleDTO dto = new()
                    {
                        ExternalId = res.Id,
                        RecurringRuleId = res.RecurringRuleId,
                        Description = res.Description,
                        Amount = res.Amount,
                        Type = (TransactionType)res.Type,
                        CategoryId = localCategoryId,
                        CategoryExternalId = res.CategoryId,
                        AccountId = localAccountId,
                        Frequency = (Frequency)res.Frequency,
                        StartDate = res.StartDate,
                        EndDate = res.EndDate,
                        Inactive = res.Inactive,
                        CreatedAt = res.CreatedAt,
                        UpdatedAt = res.UpdatedAt,
                        UserId = uid,
                    };

                    await recurringRuleRepo.UpsertAsync(dto);
                    upsertedRules.Add(dto);
                }

                DateTime batchMax = results.Max(r => r.UpdatedAt);
                if (batchMax > maxServerTs)
                    maxServerTs = batchMax;

                if (results.Count < pageSize) break;
                page++;
            }

            // Fix #2: only run the scheduler for rules that are active — inactive rules have
            // had their occurrences soft-deleted already; regenerating them would be wasteful
            // and could re-create occurrences that were intentionally cancelled.
            var activeUpserted = upsertedRules.Where(r => !r.Inactive).ToList();
            if (activeUpserted.Count > 0)
                await recurringScheduler.GeneratePendingAsync(activeUpserted);

            // Advance the cursor using the highest server-side UpdatedAt seen across all pages.
            if (maxServerTs > DateTime.MinValue)
            {
                DateTime current = await syncCursorRepo.GetAsync(SyncCursorKeys.RecurringRule);
                if (maxServerTs > current)
                    await syncCursorRepo.SaveAsync(SyncCursorKeys.RecurringRule, maxServerTs);
            }
        }

        // ── helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Converts past recurring occurrences (Date < cutoffDate) into standalone transactions
        /// by removing their RecurringRuleId link. This preserves them when the rule's StartDate
        /// is moved forward, ensuring they survive sync operations.
        /// </summary>
        private async Task ConvertPastOccurrencesToStandaloneAsync(Guid recurringRuleId, DateTime cutoffDate, bool isOnline)
        {
            var allOccurrences = await transactionService.GetByRecurringRuleIdAsync(recurringRuleId);
            var pastOccurrences = allOccurrences.Where(o => o.Date < cutoffDate && !o.Inactive).ToList();

            foreach (var occurrence in pastOccurrences)
            {
                // Remove the recurring rule link to make it a standalone transaction
                occurrence.RecurringRuleId = null;
                occurrence.Repetition = Repetition.None;
                occurrence.UpdatedAt = DateTime.Now;
                // Push to server immediately if online to maintain consistency
                await transactionService.UpdateAsync(occurrence, isOnline);
            }
        }

        private async Task PushRuleAsync(RecurringRuleDTO rule)
        {
            RecurringRuleReq req = new()
            {
                RecurringRuleId = rule.RecurringRuleId,
                Description = rule.Description,
                Amount = rule.Amount,
                Type = (int)rule.Type,
                CategoryId = rule.CategoryExternalId,
                AccountId = rule.AccountId,
                Frequency = (int)rule.Frequency,
                StartDate = rule.StartDate,
                EndDate = rule.EndDate,
                Inactive = rule.Inactive,
                UpdatedAt = rule.UpdatedAt,
            };

            int serverId = await recurringRuleApiRepo.PostAsync(req);
            rule.ExternalId = serverId;
            await recurringRuleRepo.UpdateAsync(rule);
        }
    }
}
