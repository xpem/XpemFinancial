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
        Task<IEnumerable<TransactionDTO>> GetByMonthYear(DateTime monthYear, int? accountId = null);
        Task<decimal> GetPreviousBalanceAsync(DateTime monthYear, int? accountId = null);
        Task<decimal?> GetBalanceAsync(int accountId);
        Task<TransactionDTO> GetByIdAsync(int id);
        Task<IEnumerable<TransactionDTO>> GetByRecurringRuleIdAsync(Guid recurringRuleId);
        Task DeleteAsync(int transactionId, bool isOnline);
        Task DeleteFutureOccurrencesAsync(Guid recurringRuleId, DateTime fromDate);
        Task PullAsync(int uid, DateTime lastUpdatedAt);
        Task PushPendingAsync(int userId);
        Task ResetStuckPushingAsync();
        Task<List<TransactionDescriptionRes>> GetDescriptionSuggestionsAsync(string description);

        Task AssignAccountToOrphansAsync(int accountId);
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
            TransactionDTO? existing = null;

            // 1. Try to match by TransactionId first (if non-empty)
            if (transaction.TransactionId != Guid.Empty)
            {
                existing = await transactionRepo.GetByTransactionIdAsync(transaction.TransactionId);
            }

            // 2. If no match by TransactionId, fall back to ExternalId lookup
            if (existing is null && transaction.ExternalId.HasValue)
            {
                existing = await transactionRepo.GetByExternalIdAsync(transaction.ExternalId.Value);
            }

            if (existing is not null)
            {
                // Se o registro local está sendo pushado neste momento, não sobrescrever
                if (existing.SyncStatus == TransactionSyncStatus.Pushing)
                {
                    System.Diagnostics.Debug.WriteLine($"[TransactionService] Skipping pull of transaction {existing.Id}: currently being pushed.");
                    return;
                }

                // Last-writer-wins: update only if pulled UpdatedAt > local UpdatedAt
                if (existing.UpdatedAt < transaction.UpdatedAt)
                {
                    transaction.Id = existing.Id;
                    transaction.SyncStatus = TransactionSyncStatus.Synced;
                    await transactionRepo.Update(transaction);
                }
            }
            else
            {
                // No match by TransactionId or ExternalId — insert new record
                transaction.SyncStatus = TransactionSyncStatus.Synced;
                await transactionRepo.Add(transaction);
            }
        }

        public async Task AddAsync(TransactionDTO transaction, bool isOnline)
        {
            // Transfer-specific: force fields and validate
            if (transaction.Type == TransactionType.Transfer)
            {
                transaction.Amount = -Math.Abs(transaction.Amount);
                transaction.CategoryId = null;
                transaction.Repetition = Repetition.None;

                if (transaction.Amount == 0)
                    throw new ArgumentException("Transfer amount must not be zero.");

                if (!transaction.DestinationAccountId.HasValue)
                    throw new ArgumentException("Transfer must have a DestinationAccountId.");

                if (transaction.DestinationAccountId == transaction.AccountId)
                    throw new ArgumentException("Origin and destination accounts must be different.");
            }

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

                    if (installment.TransactionId == Guid.Empty)
                        installment.TransactionId = Guid.NewGuid();

                    await transactionRepo.Add(installment);

                    if (isOnline)
                        await PushAsync(installment);
                    else
                        await transactionRepo.SetSyncStatusAsync(installment.Id, TransactionSyncStatus.Pending);
                }

                await RecalculateAccountBalanceAsync(transaction.AccountId);
            }
            else
            {
                transaction.CreatedAt = DateTime.Now;
                transaction.UpdatedAt = DateTime.Now;

                if (transaction.TransactionId == Guid.Empty)
                    transaction.TransactionId = Guid.NewGuid();

                await transactionRepo.Add(transaction);

                if (isOnline)
                    await PushAsync(transaction);
                else
                    await transactionRepo.SetSyncStatusAsync(transaction.Id, TransactionSyncStatus.Pending);

                await RecalculateAccountBalanceAsync(transaction.AccountId);

                // For transfers, also recalculate the destination account balance
                if (transaction.Type == TransactionType.Transfer)
                    await RecalculateAccountBalanceAsync(transaction.DestinationAccountId);
            }
        }

        public async Task AddOccurrenceAsync(TransactionDTO occurrence)
        {
            occurrence.CreatedAt = DateTime.Now;
            occurrence.UpdatedAt = DateTime.Now;
            occurrence.SyncStatus = TransactionSyncStatus.Pending;

            if (occurrence.TransactionId == Guid.Empty)
                occurrence.TransactionId = Guid.NewGuid();

            await transactionRepo.Add(occurrence);
        }

        public async Task UpdateAsync(TransactionDTO transaction, bool isOnline)
        {
            // Capture old state before update to detect changes
            var existing = await transactionRepo.GetByIdAsync(transaction.Id);
            int? oldAccountId = existing?.AccountId;
            int? oldDestinationAccountId = existing?.DestinationAccountId;
            var oldType = existing?.Type;

            // ═══════════════════════════════════════════════════════════════════════
            // TRANSFER LOGIC: Enforce invariants and handle type transitions
            // ═══════════════════════════════════════════════════════════════════════

            bool wasTransfer = oldType == TransactionType.Transfer;
            bool isTransfer = transaction.Type == TransactionType.Transfer;

            if (isTransfer)
            {
                // Enforce transfer field invariants
                transaction.Amount = -Math.Abs(transaction.Amount);
                transaction.CategoryId = null;
                transaction.Repetition = Repetition.None;

                // Validate DestinationAccountId
                if (!transaction.DestinationAccountId.HasValue)
                    throw new ArgumentException("Transfer must have a DestinationAccountId.");

                if (transaction.DestinationAccountId == transaction.AccountId)
                    throw new ArgumentException("Origin and destination accounts must be different.");
            }
            else if (wasTransfer && !isTransfer)
            {
                // Type changed FROM Transfer to another type: clear DestinationAccountId
                transaction.DestinationAccountId = null;
            }

            transaction.UpdatedAt = DateTime.Now;
            await transactionRepo.Update(transaction);

            // Recalculate balance for affected origin account(s)
            await RecalculateAccountBalanceAsync(transaction.AccountId);
            if (oldAccountId.HasValue && oldAccountId != transaction.AccountId)
                await RecalculateAccountBalanceAsync(oldAccountId);

            // ═══════════════════════════════════════════════════════════════════════
            // TRANSFER: Recalculate destination account balances
            // ═══════════════════════════════════════════════════════════════════════

            if (wasTransfer && !isTransfer)
            {
                // Type changed FROM Transfer: revert impact on old destination account
                await RecalculateAccountBalanceAsync(oldDestinationAccountId);
            }
            else if (isTransfer)
            {
                // Recalculate old destination if it changed
                if (oldDestinationAccountId.HasValue && oldDestinationAccountId != transaction.DestinationAccountId)
                    await RecalculateAccountBalanceAsync(oldDestinationAccountId);

                // Always recalculate current destination account
                await RecalculateAccountBalanceAsync(transaction.DestinationAccountId);
            }

            if (!isOnline)
            {
                // Marca como pendente para push no próximo ciclo de sync
                await transactionRepo.SetSyncStatusAsync(transaction.Id, TransactionSyncStatus.Pending);
                return;
            }

            // Customized occurrence without a server record yet → POST to create it.
            if (transaction.IsCustomized && !transaction.ExternalId.HasValue)
            {
                await PushAsync(transaction);
                return;
            }

            AccountDTO? txAccount = transaction.AccountId.HasValue ? await accountRepo.GetByIdAsync(transaction.AccountId.Value) : null;
            int? accountExternalId = transaction.AccountExternalId ?? txAccount?.ExternalId;

            if (accountExternalId is null)
            {
                // Conta não sincronizada — adiar push para próximo ciclo
                System.Diagnostics.Debug.WriteLine($"[TransactionService] Deferring push of transaction {transaction.Id}: account has no ExternalId.");
                return;
            }

            // Resolve DestinationAccountExternalId para o PUT
            int? destAccountExternalId = null;
            if (transaction.DestinationAccountId.HasValue)
            {
                var destAccount = await accountRepo.GetByIdAsync(transaction.DestinationAccountId.Value);
                destAccountExternalId = transaction.DestinationAccountExternalId ?? destAccount?.ExternalId;

                if (destAccountExternalId is null)
                {
                    // Conta destino não sincronizada — adiar push para próximo ciclo
                    System.Diagnostics.Debug.WriteLine($"[TransactionService] Deferring push of transaction {transaction.Id}: destination account has no ExternalId.");
                    await transactionRepo.SetSyncStatusAsync(transaction.Id, TransactionSyncStatus.Pending);
                    return;
                }
            }

            if (transaction.ExternalId.HasValue)
            {
                await transactionRepo.SetSyncStatusAsync(transaction.Id, TransactionSyncStatus.Pushing);

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
                    AccountId = accountExternalId.Value,
                    DestinationAccountId = destAccountExternalId,
                    RecurringRuleId = transaction.RecurringRuleId,
                    IsCustomized = transaction.IsCustomized,
                    TransactionId = transaction.TransactionId == Guid.Empty ? null : transaction.TransactionId,
                };

                try
                {
                    await transactionApiRepo.PutAsync(transaction.ExternalId.Value, req);
                    await transactionRepo.SetSyncStatusAsync(transaction.Id, TransactionSyncStatus.Synced);
                }
                catch
                {
                    await transactionRepo.SetSyncStatusAsync(transaction.Id, TransactionSyncStatus.Pending);
                    throw;
                }
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

            // For transfers, explicitly ensure the destination account balance is recalculated.
            // After setting Inactive = true, GetSumByAccountIdAsync will exclude this transaction,
            // effectively reverting the transfer's impact on the destination account.
            if (transaction.Type == TransactionType.Transfer && transaction.DestinationAccountId.HasValue)
            {
                await RecalculateAccountBalanceAsync(transaction.DestinationAccountId);
            }
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
            // ═══════════════════════════════════════════════════════════════════════
            // PROTEÇÃO CONTRA DUPLICAÇÃO: Verifica se já foi sincronizado
            // ═══════════════════════════════════════════════════════════════════════
            if (transaction.ExternalId.HasValue)
            {
                System.Diagnostics.Debug.WriteLine($"[TransactionService] Skipping push of transaction {transaction.Id}: already has ExternalId {transaction.ExternalId}.");
                return;
            }

            // AccountExternalId is [NotMapped] — it is not persisted in SQLite and is null
            // when a scheduler-generated occurrence is loaded from the local database.
            // Resolve it from the account record before building the API request.
            AccountDTO? txAccount = transaction.AccountId.HasValue
                ? await accountRepo.GetByIdAsync(transaction.AccountId.Value)
                : null;
            int? accountExternalId = transaction.AccountExternalId
                ?? txAccount?.ExternalId;

            if (accountExternalId is null)
            {
                // Conta não sincronizada — adiar push para próximo ciclo
                System.Diagnostics.Debug.WriteLine($"[TransactionService] Deferring push of transaction {transaction.Id}: account has no ExternalId.");
                await transactionRepo.SetSyncStatusAsync(transaction.Id, TransactionSyncStatus.Pending);
                return;
            }

            // CategoryExternalId is [NotMapped] and won't be available when the transaction
            // is loaded from GetPendingPushAsync (no .Include). Resolve it from the local
            // CategoryId the same way we resolve AccountExternalId above.
            CategoryDTO? txCategory = transaction.CategoryId.HasValue
                ? await categoryRepo.GetByIdAsync(transaction.CategoryId.Value)
                : null;
            int? categoryExternalId = transaction.CategoryExternalId
                ?? transaction.Category?.ExternalId
                ?? txCategory?.ExternalId;

            // ═══════════════════════════════════════════════════════════════════════
            // Resolve DestinationAccountExternalId (mesma lógica de AccountExternalId)
            // ═══════════════════════════════════════════════════════════════════════
            int? destinationAccountExternalId = null;
            if (transaction.DestinationAccountId.HasValue)
            {
                var destAccount = await accountRepo.GetByIdAsync(transaction.DestinationAccountId.Value);
                destinationAccountExternalId = transaction.DestinationAccountExternalId ?? destAccount?.ExternalId;

                if (destinationAccountExternalId is null)
                {
                    // Conta destino não sincronizada — adiar push para próximo ciclo
                    System.Diagnostics.Debug.WriteLine($"[TransactionService] Deferring push of transaction {transaction.Id}: destination account has no ExternalId.");
                    await transactionRepo.SetSyncStatusAsync(transaction.Id, TransactionSyncStatus.Pending);
                    return;
                }
            }

            // ═══════════════════════════════════════════════════════════════════════
            // Marca como Pushing — pull deve ignorar este registro enquanto estiver neste estado
            // ═══════════════════════════════════════════════════════════════════════
            await transactionRepo.SetSyncStatusAsync(transaction.Id, TransactionSyncStatus.Pushing);

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
                AccountId = accountExternalId.Value,
                DestinationAccountId = destinationAccountExternalId,
                RecurringRuleId = transaction.RecurringRuleId,
                IsCustomized = transaction.IsCustomized,
                TransactionId = transaction.TransactionId == Guid.Empty ? null : transaction.TransactionId,
            };

            int serverId;
            try
            {
                serverId = await transactionApiRepo.PostAsync(req);
            }
            catch
            {
                // Falha na API — volta para Pending para retry no próximo ciclo
                await transactionRepo.SetSyncStatusAsync(transaction.Id, TransactionSyncStatus.Pending);
                throw;
            }

            // ═══════════════════════════════════════════════════════════════════════
            // PROTEÇÃO CONTRA DUPLICAÇÃO: Recarrega do banco antes de atualizar
            // para evitar sobrescrever um ExternalId já salvo por outro thread
            // ═══════════════════════════════════════════════════════════════════════
            var freshCopy = await transactionRepo.GetByIdAsync(transaction.Id);
            if (freshCopy.ExternalId.HasValue)
            {
                System.Diagnostics.Debug.WriteLine($"[TransactionService] Transaction {transaction.Id} was already updated with ExternalId {freshCopy.ExternalId} by another process.");
                await transactionRepo.SetSyncStatusAsync(transaction.Id, TransactionSyncStatus.Synced);
                return;
            }

            await transactionRepo.SetExternalIdAndSyncedAsync(transaction.Id, serverId);
        }

        public async Task<TransactionDTO> GetByIdAsync(int id)
        {
            return await transactionRepo.GetByIdAsync(id);
        }

        public async Task<IEnumerable<TransactionDTO>> GetByMonthYear(DateTime monthYear, int? accountId = null)
        {
            return await transactionRepo.GetByMonthYear(monthYear, accountId);
        }

        //calculo do saldo anteior, que é o total das transações até o inicio do mes selecionado, considera todas as transações de ajuste, entrada e saída.
        public async Task<decimal> GetPreviousBalanceAsync(DateTime monthYear, int? accountId = null)
        {
            return await transactionRepo.GetPreviousBalanceAsync(monthYear, accountId);
        }

        public async Task<decimal?> GetBalanceAsync(int accountId)
        {
            //calcula até o primeiro dia do próximo mês(calcula o saldo atual, considerando todas as transações até o final do mês atual)
            DateTime now = DateTime.Now;
            DateTime nextMonth = new DateTime(now.Year, now.Month, 1).AddMonths(1);

            return await transactionRepo.GetBalanceAsync(accountId, nextMonth);
        }

        public async Task PushPendingAsync(int userId)
        {
            var pending = await transactionRepo.GetPendingPushAsync(userId);

            foreach (var transaction in pending)
            {
                try
                {
                    await PushAsync(transaction);
                }
                catch
                {
                    // Push individual falhou — manter ExternalId atual, tentar no próximo ciclo.
                    continue;
                }
            }
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

                // Resolve o DestinationAccountId local a partir do ExternalId da conta destino (com cache)
                int? localDestinationAccountId = null;
                if (t.DestinationAccountId.HasValue)
                {
                    if (!accountCache.TryGetValue(t.DestinationAccountId.Value, out int destLocalId))
                    {
                        destLocalId = await accountRepo.GetLocalIdByExternalIdAsync(t.DestinationAccountId.Value);
                        accountCache[t.DestinationAccountId.Value] = destLocalId;
                    }
                    localDestinationAccountId = destLocalId > 0 ? destLocalId : null;
                }

                TransactionDTO dto = new()
                {
                    ExternalId = t.Id,
                    TransactionId = t.TransactionId ?? Guid.Empty,
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
                    DestinationAccountId = localDestinationAccountId,
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

        private async Task RecalculateAccountBalanceAsync(int? accountId)
        {
            if (!accountId.HasValue) return;

            var account = await accountRepo.GetByIdAsync(accountId.Value);
            if (account is null) return;

            decimal sum = await transactionRepo.GetSumByAccountIdAsync(accountId.Value);
            account.CurrentBalance = sum;
            account.UpdatedAt = DateTime.UtcNow;
            await accountRepo.Update(account);
        }

        public async Task AssignAccountToOrphansAsync(int accountId) => await transactionRepo.AssignAccountToOrphansAsync(accountId);

        public async Task ResetStuckPushingAsync() => await transactionRepo.ResetStuckPushingAsync();
    }
}
