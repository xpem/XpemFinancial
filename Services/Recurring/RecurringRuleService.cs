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
        Task RunSchedulerAsync();
        Task<IEnumerable<RecurringRuleDTO>> GetAllActiveAsync();
        Task PullAsync(int uid);
    }

    public class RecurringRuleService(
        Repo.IRecurringRuleRepo recurringRuleRepo,
        IRecurringScheduler recurringScheduler,
        IRecurringRuleApiRepo recurringRuleApiRepo,
        ITransactionService transactionService,
        ICategoryRepo categoryRepo) : IRecurringRuleService
    {
        public async Task<ServiceResp> SaveAsync(RecurringRuleDTO rule, bool isOnline)
        {
            // Validation (task 6.2)
            if (rule.Amount <= 0)
                return new ServiceResp(false, "Amount must be greater than zero.");

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
            await recurringScheduler.GeneratePendingAsync([rule], 3);

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
                        var occurrence = await transactionService.GetByIdAsync(req.TransactionId);
                        occurrence.Description = req.UpdatedRule.Description ?? occurrence.Description;
                        occurrence.Amount      = req.UpdatedRule.Amount;
                        occurrence.CategoryId  = req.UpdatedRule.CategoryId;
                        occurrence.Type        = req.UpdatedRule.Type;
                        occurrence.UpdatedAt   = DateTime.Now;
                        await transactionService.UpdateAsync(occurrence);
                        break;
                    }

                    case EditScope.ThisAndFuture:
                    {
                        var rule           = await recurringRuleRepo.GetByIdAsync(req.RecurringRuleId)
                                             ?? throw new InvalidOperationException($"Rule {req.RecurringRuleId} not found.");
                        var targetOccurrence = await transactionService.GetByIdAsync(req.TransactionId);

                        rule.Description = req.UpdatedRule.Description;
                        rule.Amount      = req.UpdatedRule.Amount;
                        rule.Type        = req.UpdatedRule.Type;
                        rule.CategoryId  = req.UpdatedRule.CategoryId;
                        rule.AccountId   = req.UpdatedRule.AccountId;
                        rule.Frequency   = req.UpdatedRule.Frequency;
                        rule.EndDate     = req.UpdatedRule.EndDate;
                        rule.UpdatedAt   = DateTime.Now;

                        await transactionService.DeleteFutureOccurrencesAsync(req.RecurringRuleId, targetOccurrence.Date);
                        await recurringRuleRepo.UpdateAsync(rule);
                        await recurringScheduler.GeneratePendingAsync([rule], 3);

                        if (isOnline)
                            await PushRuleAsync(rule);

                        break;
                    }

                    case EditScope.All:
                    {
                        var rule = await recurringRuleRepo.GetByIdAsync(req.RecurringRuleId)
                                   ?? throw new InvalidOperationException($"Rule {req.RecurringRuleId} not found.");

                        rule.Description = req.UpdatedRule.Description;
                        rule.Amount      = req.UpdatedRule.Amount;
                        rule.Type        = req.UpdatedRule.Type;
                        rule.CategoryId  = req.UpdatedRule.CategoryId;
                        rule.AccountId   = req.UpdatedRule.AccountId;
                        rule.Frequency   = req.UpdatedRule.Frequency;
                        rule.EndDate     = req.UpdatedRule.EndDate;
                        rule.UpdatedAt   = DateTime.Now;

                        await recurringRuleRepo.UpdateAsync(rule);

                        var occurrences = await transactionService.GetByRecurringRuleIdAsync(req.RecurringRuleId);
                        foreach (var occurrence in occurrences.Where(o => !o.Inactive))
                        {
                            occurrence.Description = req.UpdatedRule.Description ?? occurrence.Description;
                            occurrence.Amount      = req.UpdatedRule.Amount;
                            occurrence.Type        = req.UpdatedRule.Type;
                            occurrence.CategoryId  = req.UpdatedRule.CategoryId;
                            occurrence.UpdatedAt   = DateTime.Now;
                            await transactionService.UpdateAsync(occurrence);
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
                        var rule             = await recurringRuleRepo.GetByIdAsync(req.RecurringRuleId)
                                               ?? throw new InvalidOperationException($"Rule {req.RecurringRuleId} not found.");
                        var targetOccurrence = await transactionService.GetByIdAsync(req.TransactionId);

                        rule.EndDate   = targetOccurrence.Date.AddDays(-1);
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

                        rule.Inactive  = true;
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

        public async Task RunSchedulerAsync()
        {
            IEnumerable<RecurringRuleDTO> rules = await GetAllActiveAsync();
            await recurringScheduler.GeneratePendingAsync(rules, 3);
        }

        public async Task<IEnumerable<RecurringRuleDTO>> GetAllActiveAsync()
        {
            return await recurringRuleRepo.GetAllActiveAsync();
        }

        public async Task PullAsync(int uid)
        {
            DateTime lastUpdatedAt = await recurringRuleRepo.GetMaxUpdatedAtAsync();

            List<Model.Resp.Api.RecurringRuleApiRes>? results = await recurringRuleApiRepo.GetByUpdatedAtAsync(lastUpdatedAt);

            if (results is null || results.Count == 0) return;

            List<RecurringRuleDTO> upsertedRules = [];

            foreach (var res in results)
            {
                // Resolve local CategoryId from the API's CategoryId (which is the ExternalId)
                int localCategoryId = 0;
                if (res.CategoryId.HasValue)
                {
                    var localCategory = await categoryRepo.GetByExternalIdAsync(res.CategoryId.Value);
                    localCategoryId = localCategory?.Id ?? 0;
                }

                RecurringRuleDTO dto = new()
                {
                    ExternalId        = res.Id,
                    RecurringRuleId   = res.RecurringRuleId,
                    Description       = res.Description,
                    Amount            = res.Amount,
                    Type              = (TransactionType)res.Type,
                    CategoryId        = localCategoryId,
                    CategoryExternalId = res.CategoryId,
                    AccountId         = res.AccountId,
                    Frequency         = (Frequency)res.Frequency,
                    StartDate         = res.StartDate,
                    EndDate           = res.EndDate,
                    Inactive          = res.Inactive,
                    CreatedAt         = res.CreatedAt,
                    UpdatedAt         = res.UpdatedAt,
                    UserId            = uid,
                };

                await recurringRuleRepo.UpsertAsync(dto);
                upsertedRules.Add(dto);
            }

            await recurringScheduler.GeneratePendingAsync(upsertedRules, 3);
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private async Task PushRuleAsync(RecurringRuleDTO rule)
        {
            RecurringRuleReq req = new()
            {
                RecurringRuleId = rule.RecurringRuleId,
                Description     = rule.Description,
                Amount          = rule.Amount,
                Type            = (int)rule.Type,
                CategoryId      = rule.CategoryExternalId,
                AccountId       = rule.AccountId,
                Frequency       = (int)rule.Frequency,
                StartDate       = rule.StartDate,
                EndDate         = rule.EndDate,
                Inactive        = rule.Inactive,
                UpdatedAt       = rule.UpdatedAt,
            };

            int serverId = await recurringRuleApiRepo.PostAsync(req);
            rule.ExternalId = serverId;
            await recurringRuleRepo.UpdateAsync(rule);
        }
    }
}
