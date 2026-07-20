using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Service;
using Service.Account;
using Service.Category;
using Service.Recurring;
using Service.Transaction;
using XpemFinancial.Messages;
using XpemFinancial.Utils.Services;
using XpemFinancial.Views;

namespace XpemFinancial.VMs
{
    public partial class FirstSyncProcessVM(
        IUserSessionService userSessionService,
        ICategoryService categoryService,
        IAccountService accountService,
        IRecurringRuleService recurringRuleService,
        ITransactionService transactionService,
        SyncService syncService) : VMBase
    {
        [ObservableProperty] private double progress;

        public async Task SyncProcess()
        {
            if (IsBusy) return;

            IsBusy = true;

            try
            {
                var user = await userSessionService.GetCurrentUserAsync();
                DateTime mindate = DateTime.MinValue;

                if (user == null)
                {
                    await ShowMessage("Erro", "Usuário não encontrado. Faça login novamente.");
                    return;
                }

                if (Connectivity.NetworkAccess != NetworkAccess.Internet)
                {
                    await ShowMessage("Erro", "Sem conexão com a internet.");
                    return;
                }

                await categoryService.PullAsync(user.Id, mindate);
                Progress = 0.2;

                await accountService.PullAsync(user.Id);
                Progress = 0.4;

                await recurringRuleService.PullAsync(user.Id, mindate);
                Progress = 0.6;

                await transactionService.PullAsync(user.Id, mindate);
                Progress = 0.8;

                Progress = 1;

                WeakReferenceMessenger.Default.Send(new UserLoggedInMessage());

                _ = Task.Run(() => { Task.Delay(5000); syncService.StartThread(); });
                               
                await Shell.Current.GoToAsync($"//{nameof(MainPage)}");
            }
            catch (UnauthorizedAccessException)
            {
                await ShowMessage("Sessão expirada", "Sessão expirada. Faça login novamente.");
                await Shell.Current.GoToAsync($"//{nameof(SignInPage)}");
            }
            catch (Exception ex)
            {
                await ShowMessage("Erro", $"Erro ao sincronizar: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
