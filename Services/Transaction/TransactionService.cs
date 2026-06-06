using ApiRepo;
using Model.DTO;
using Model.Req;
using Model.Res;
using Model.Resp.Api;
using Repo;
using System;
using System.Collections.Generic;
using System.Text;
namespace Service.Transaction
{
    public interface ITransactionService
    {
        Task AddAsync(TransactionDTO transaction, bool isOnline);
        Task AddOccurrenceAsync(TransactionDTO occurrence);
        Task UpdateAsync(TransactionDTO transaction, bool isOnline);
        Task ApplyFromApiAsync(TransactionDTO transaction);
        Task<DateTime> GetLastUpdatedAtAsync();
        Task<IEnumerable<TransactionDTO>> GetByMonthYear(DateTime monthYear);
        Task<decimal> GetPreviousBalanceAsync(DateTime monthYear);
        Task<decimal?> GetBalanceAsync(int accountId);
        Task<TransactionDTO> GetByIdAsync(int id);
        Task<IEnumerable<TransactionDTO>> GetByRecurringRuleIdAsync(Guid recurringRuleId);
        Task DeleteAsync(int transactionId, bool isOnline);
        Task DeleteFutureOccurrencesAsync(Guid recurringRuleId, DateTime fromDate);
        Task PullAsync(int uid, DateTime lastUpdatedAt);
        Task<List<TransactionDescriptionRes>> GetDescriptionSuggestionsAsync(string description);
    }

    public class TransactionService(ITransactionRepo transactionRepo, ITransactionApiRepo transactionApiRepo, ICategoryRepo categoryRepo, IAccountRepo accountRepo, ISyncCursorRepo syncCursorRepo) : ITransactionService
    {
        public async Task<DateTime> GetLastUpdatedAtAsync()
        {
            // Use the server-side cursor so the delta query is anchored to the server's clock,
            // not the device clock (avoids skipping records when the device clock is behind).
            return await syncCursorRepo.GetAsync(SyncCursorKeys.Transaction);
        }

        public async Task ApplyFromApiAsync(TransactionDTO transaction)
        {
            var existing = await transactionRepo.GetByExternalIdAsync(transaction.ExternalId!.Value);

            if (existing is not null)
            {
                if (existing.UpdatedAt < transaction.UpdatedAt)
                {
                    transaction.Id = existing.Id;
                    await transactionRepo.Update(transaction);
                }
            }
            else
            {
                await transactionRepo.Add(transaction);
            }
        }

        public async Task AddAsync(TransactionDTO transaction, bool isOnline)
        {
            if (transaction.Repetition == Repetition.Monthly)
            {
                if (transaction.TotalInstallments == null || transaction.TotalInstallments <= 0)
                    throw new ArgumentException("TotalInstallments must be greater than 0 for monthly transactions.");

                if (transaction.Installment != null && transaction.Installment > transaction.TotalInstallments)
                    throw new ArgumentException("Installment number cannot be greater than TotalInstallments.");

                if (transaction.InstallmentId == null)
                    transaction.InstallmentId = Guid.NewGuid();

                for (int i = transaction.Installment!.Value; i <= transaction.TotalInstallments; i++)
                {
                    var installment = new TransactionDTO
                    {
                        Description = transaction.Description,
                        Date = transaction.Date.AddMonths(i - 1),
                        Amount = transaction.Amount,
                        Repetition = transaction.Repetition,
                        TotalInstallments = transaction.TotalInstallments,
                        InstallmentId = transaction.InstallmentId,
                        Installment = i,
                        CategoryId = transaction.CategoryId,
                        CategoryExternalId = transaction.CategoryExternalId,
                        Type = transaction.Type,
                        Note = transaction.Note,
                        AccountId = transaction.AccountId,
                        AccountExternalId = transaction.AccountExternalId,
                        UserId = transaction.UserId,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                    };

                    await transactionRepo.Add(installment);

                    if (isOnline)
                        await PushAsync(installment);
                }
            }
            else
            {
                transaction.CreatedAt = DateTime.Now;
                transaction.UpdatedAt = DateTime.Now;

                await transactionRepo.Add(transaction);

                if (isOnline)
                    await PushAsync(transaction);
            }
        }

        public async Task AddOccurrenceAsync(TransactionDTO occurrence)
        {
            occurrence.CreatedAt = DateTime.Now;
            occurrence.UpdatedAt = DateTime.Now;
            await transactionRepo.Add(occurrence);
        }

        public async Task UpdateAsync(TransactionDTO transaction, bool isOnline)
        {
            transaction.UpdatedAt = DateTime.Now;
            await transactionRepo.Update(transaction);

            if (!isOnline) return;

            // Customized occurrence without a server record yet → POST to create it.
            if (transaction.IsCustomized && !transaction.ExternalId.HasValue)
            {
                await PushAsync(transaction);
                return;
            }

            if (transaction.ExternalId.HasValue)
            {
                TransactionReq req = new()
                {
                    UpdatedAt = transaction.UpdatedAt,
                    Inactive = transaction.Inactive,
                    Description = transaction.Description,
                    Date = transaction.Date,
                    Amount = transaction.Amount,
                    Repetition = transaction.Repetition,
                    TotalInstallments = transaction.TotalInstallments,
                    InstallmentId = transaction.InstallmentId,
                    Installment = transaction.Installment,
                    CategoryId = transaction.CategoryExternalId,
                    Type = transaction.Type,
                    Note = transaction.Note,
                    AccountId = transaction.AccountId ?? 0,
                    RecurringRuleId = transaction.RecurringRuleId,
                    IsCustomized = transaction.IsCustomized,
                };

                await transactionApiRepo.PutAsync(transaction.ExternalId.Value, req);
            }
        }

        public async Task<IEnumerable<TransactionDTO>> GetByRecurringRuleIdAsync(Guid recurringRuleId)
        {
            return await transactionRepo.GetByRecurringRuleIdAsync(recurringRuleId);
        }

        public async Task DeleteAsync(int transactionId, bool isOnline)
        {
            var transaction = await transactionRepo.GetByIdAsync(transactionId);
            if (transaction is null) return;

            transaction.Inactive = true;
            await UpdateAsync(transaction, isOnline);
        }

        public async Task DeleteFutureOccurrencesAsync(Guid recurringRuleId, DateTime fromDate)
        {
            var occurrences = await GetByRecurringRuleIdAsync(recurringRuleId);
            foreach (var occurrence in occurrences.Where(o => o.Date >= fromDate))
            {
                occurrence.Inactive = true;
                occurrence.UpdatedAt = DateTime.Now;
                await transactionRepo.Update(occurrence);
            }
        }

        private async Task PushAsync(TransactionDTO transaction)
        {
            // AccountExternalId is [NotMapped] — it is not persisted in SQLite and is null
            // when a scheduler-generated occurrence is loaded from the local database.
            // Resolve it from the account record before building the API request.
            int accountExternalId = transaction.AccountExternalId
                ?? (await accountRepo.GetAsync())?.ExternalId
                ?? throw new InvalidOperationException(
                    $"Cannot push transaction {transaction.Id}: account has no ExternalId. " +
                    "Ensure the account was synced before pushing transactions.");

            // CategoryExternalId is also [NotMapped]. Use the navigation property loaded by
            // GetByIdAsync (which does .Include(Category)), or fall back to CategoryExternalId
            // if already set (e.g. on a freshly created transaction that hasn't been reloaded).
            int? categoryExternalId = transaction.CategoryExternalId
                ?? transaction.Category?.ExternalId;

            TransactionReq req = new()
            {
                UpdatedAt = transaction.UpdatedAt,
                Inactive = transaction.Inactive,
                Description = transaction.Description,
                Date = transaction.Date,
                Amount = transaction.Amount,
                Repetition = transaction.Repetition,
                TotalInstallments = transaction.TotalInstallments,
                InstallmentId = transaction.InstallmentId,
                Installment = transaction.Installment,
                CategoryId = categoryExternalId,
                Type = transaction.Type,
                Note = transaction.Note,
                AccountId = accountExternalId,
                RecurringRuleId = transaction.RecurringRuleId,
                IsCustomized = transaction.IsCustomized,
            };

            int serverId = await transactionApiRepo.PostAsync(req);

            transaction.ExternalId = serverId;
            await transactionRepo.Update(transaction);
        }

        public async Task<TransactionDTO> GetByIdAsync(int id)
        {
            return await transactionRepo.GetByIdAsync(id);
        }

        public async Task<IEnumerable<TransactionDTO>> GetByMonthYear(DateTime monthYear)
        {
            // Implement the logic to get transactions by month and year
            return await transactionRepo.GetByMonthYear(monthYear);
        }

        //calculo do saldo anteior, que é o total das transações até o inicio do mes selecionado, considera todas as transações de ajuste, entrada e saída.
        public async Task<decimal> GetPreviousBalanceAsync(DateTime monthYear)
        {
            return await transactionRepo.GetPreviousBalanceAsync(monthYear);
        }

        public async Task<decimal?> GetBalanceAsync(int accountId)
        {
            //calcula até o primeiro dia do próximo mês(calcula o saldo atual, considerando todas as transações até o final do mês atual)
            DateTime now = DateTime.Now;
            DateTime nextMonth = new DateTime(now.Year, now.Month, 1).AddMonths(1);

            return await transactionRepo.GetBalanceAsync(accountId, nextMonth);
        }

        public async Task PullAsync(int uid, DateTime lastUpdatedAt)
        {
            List<TransactionApiRes>? apiTransactions = await transactionApiRepo.GetByUpdatedAtAsync(lastUpdatedAt);

            if (apiTransactions is null || apiTransactions.Count == 0) return;

            Dictionary<int, int> accountCache = [];
            Dictionary<int, int?> categoryCache = [];

            foreach (var t in apiTransactions)
            {
                // Resolve o CategoryId local a partir do ExternalId da categoria (com cache)
                int? localCategoryId = null;
                if (t.CategoryId.HasValue)
                {
                    if (!categoryCache.TryGetValue(t.CategoryId.Value, out localCategoryId))
                    {
                        var localCategory = await categoryRepo.GetByExternalIdAsync(t.CategoryId.Value);
                        localCategoryId = localCategory?.Id;
                        categoryCache[t.CategoryId.Value] = localCategoryId;
                    }
                }

                // Resolve o AccountId local a partir do ExternalId da conta (com cache)
                if (!accountCache.TryGetValue(t.AccountId, out int localAccountId))
                {
                    localAccountId = await accountRepo.GetLocalIdByExternalIdAsync(t.AccountId);
                    accountCache[t.AccountId] = localAccountId;
                }

                TransactionDTO dto = new()
                {
                    ExternalId = t.Id,
                    Description = t.Description,
                    Date = t.Date,
                    Amount = t.Amount,
                    Repetition = (Repetition)t.Repetition,
                    TotalInstallments = t.TotalInstallments,
                    InstallmentId = t.InstallmentId,
                    Installment = t.Installment,
                    CategoryId = localCategoryId ?? null,
                    Type = (TransactionType)t.Type,
                    Note = t.Note,
                    AccountId = localAccountId,
                    Inactive = t.Inactive,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt,
                    UserId = uid,
                    RecurringRuleId = t.RecurringRuleId,
                    IsCustomized = t.IsCustomized,
                };

                await ApplyFromApiAsync(dto);
            }

            // Advance the cursor to the highest server-side UpdatedAt in this batch.
            // Using values from the server response (not DateTime.UtcNow) keeps the anchor
            // on the server's clock regardless of any device clock skew.
            DateTime maxServerTs = apiTransactions.Max(t => t.UpdatedAt);
            DateTime current = await syncCursorRepo.GetAsync(SyncCursorKeys.Transaction);
            if (maxServerTs > current)
                await syncCursorRepo.SaveAsync(SyncCursorKeys.Transaction, maxServerTs);
        }

        public async Task<List<TransactionDescriptionRes>> GetDescriptionSuggestionsAsync(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return [];

            var results = await transactionRepo.GetTransactionDescription(description);

            // Deduplica por descrição, mantendo a mais recente (maior TransactionID)
            return results
                .GroupBy(r => r.Description, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(r => r.TransactionID).First())
                .Take(5)
                .ToList();
        }
    }
}
