using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Service;
using Service.Account;
using Service.Category;
using Service.Recurring;
using Service.Transaction;
using XpemFinancial.Messages;
using XpemFinancial.Views;

namespace XpemFinancial.VMs
{
    public partial class FirstSyncProcessVM(
        IUserSessionService userSessionService,
        ICategoryService categoryService,
        IAccountService accountService,
        IRecurringRuleService recurringRuleService,
        ITransactionService transactionService) : VMBase
    {
        [ObservableProperty] private double progress;

        public async Task SyncProcess()
        {
            if (IsBusy) return;

            IsBusy = true;

            try
            {
                var user = await userSessionService.GetCurrentUserAsync();

                if (user == null)
                {
                    await ShowMessage("Usuário não encontrado. Faça login novamente.", "Erro");
                    return;
                }

                if (Connectivity.NetworkAccess != NetworkAccess.Internet)
                {
                    await ShowMessage("Sem conexão com a internet.", "Erro");
                    return;
                }

                await categoryService.PullAsync(user.Id);
                Progress = 0.2;

                await accountService.PullAsync(user.Id);
                Progress = 0.4;

                await recurringRuleService.PullAsync(user.Id);
                Progress = 0.6;

                await transactionService.PullAsync(user.Id);
                Progress = 0.8;
                Progress = 1;

                WeakReferenceMessenger.Default.Send(new UserLoggedInMessage());
                await Shell.Current.GoToAsync($"//{nameof(MainPage)}");
            }
            catch (UnauthorizedAccessException)
            {
                await ShowMessage("Sessão expirada. Faça login novamente.", "Sessão expirada");
                await Shell.Current.GoToAsync($"//{nameof(SignInPage)}");
            }
            catch (Exception ex)
            {
                await ShowMessage($"Erro ao sincronizar: {ex.Message}", "Erro");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
