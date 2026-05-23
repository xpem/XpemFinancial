using ApiRepo;
using Model.DTO;
using Model.Req;

namespace Service.Account
{
    public interface IAccountSyncService
    {
        /// <summary>
        /// Envia o ajuste de balanço para a API e retorna os IDs gerados pelo servidor
        /// para o account e a transaction.
        /// </summary>
        Task<(int accountId, int transactionId)> PushAdjustAccountBalanceAsync(AccountDTO accountDTO, TransactionDTO adjustmentTransaction);
    }

    public class AccountSyncService(IAccountApiRepo accountApiRepo) : IAccountSyncService
    {
        public async Task<(int accountId, int transactionId)> PushAdjustAccountBalanceAsync(AccountDTO accountDTO, TransactionDTO adjustmentTransaction)
        {
            AdjustAccountBalanceReq req = new()
            {
                UpdatedAt = DateTime.UtcNow,
                Inactive = accountDTO.Inactive,
                Transaction = new TransactionReq
                {
                    Description = adjustmentTransaction.Description,
                    Date = adjustmentTransaction.Date,
                    Amount = adjustmentTransaction.Amount,
                    Repetition = adjustmentTransaction.Repetition,
                    TotalInstallments = adjustmentTransaction.TotalInstallments,
                    InstallmentId = adjustmentTransaction.InstallmentId,
                    Installment = adjustmentTransaction.Installment,
                    CategoryId = adjustmentTransaction.CategoryId,
                    Type = adjustmentTransaction.Type,
                    Note = adjustmentTransaction.Note,
                }
            };

            AdjustAccountBalanceReq result = await accountApiRepo.PostAdjustAccountBalance(req);

            int accountId = result.Id ?? throw new Exception("API não retornou o Id da conta.");
            int transactionId = result.Transaction.Id ?? throw new Exception("API não retornou o Id da transação.");

            return (accountId, transactionId);
        }
    }
}
