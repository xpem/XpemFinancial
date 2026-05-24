using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Service;
using Service.Account;
using Service.Category;
using Service.Transaction;
using XpemFinancial.Messages;
using XpemFinancial.Views;

namespace XpemFinancial.VMs
{
    public partial class FirstSyncProcessVM(
        IUserSessionService userSessionService,
        ICategoryService categoryService,
        IAccountService accountService,
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
                Progress = 0.25;

                await accountService.PullAsync(user.Id);
                Progress = 0.5;

                await transactionService.PullAsync(user.Id);
                Progress = 0.75;

                // await BookHistoricSyncBLL.ApiToLocalSync(user.Id, user.LastUpdate);
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
