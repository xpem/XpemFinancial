using ApiRepo;
using Model.DTO;
using Model.Req;
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
        Task UpsertAsync(TransactionDTO transaction);
        Task<DateTime> GetLastUpdatedAtAsync();
        Task<IEnumerable<TransactionDTO>> GetByMonthYear(DateTime monthYear);
        Task<decimal> GetPreviousBalanceAsync(DateTime monthYear);
        Task<decimal?> GetBalanceAsync(int accountId);
        Task<TransactionDTO> GetByIdAsync(int id);
        Task PullAsync(int uid);
    }

    public class TransactionService(ITransactionRepo transactionRepo, ITransactionApiRepo transactionApiRepo, ICategoryRepo categoryRepo) : ITransactionService
    {
        public async Task<DateTime> GetLastUpdatedAtAsync()
        {
            return await transactionRepo.GetMaxUpdatedAtAsync();
        }

        public async Task UpsertAsync(TransactionDTO transaction)
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
                        Type = transaction.Type,
                        Note = transaction.Note,
                        AccountId = transaction.AccountId,
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

        private async Task PushAsync(TransactionDTO transaction)
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
            return await transactionRepo.GetBalanceAsync(accountId);
        }

        public async Task PullAsync(int uid)
        {
            DateTime lastUpdatedAt = await GetLastUpdatedAtAsync();

            List<TransactionApiRes>? apiTransactions = await transactionApiRepo.GetByUpdatedAtAsync(lastUpdatedAt);

            if (apiTransactions is null || apiTransactions.Count == 0) return;

            foreach (var t in apiTransactions)
            {
                // Resolve o CategoryId local a partir do ExternalId da categoria
                int? localCategoryId = null;
                if (t.CategoryId.HasValue)
                {
                    var localCategory = await categoryRepo.GetByExternalIdAsync(t.CategoryId.Value);
                    localCategoryId = localCategory?.Id;
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
                    CategoryId = localCategoryId ?? 0,
                    Type = (TransactionType)t.Type,
                    Note = t.Note,
                    AccountId = t.AccountId,
                    Inactive = t.Inactive,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt,
                    UserId = uid,
                };

                await UpsertAsync(dto);
            }
        }
    }
}
