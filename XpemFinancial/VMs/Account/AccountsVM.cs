using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.DTO;
using Service;
using Service.Account;
using Service.Transaction;
using XpemFinancial.Views.Account;

namespace XpemFinancial.VMs;

public partial class AccountsVM(
    IAccountService accountService,
    IUserSessionService userSessionService,
    ITransactionService transactionService) : VMBase
{
    [ObservableProperty]
    private List<AccountDTO> activeAccounts = [];

    [ObservableProperty]
    private List<AccountDTO> inactiveAccounts = [];

    [ObservableProperty]
    private bool hasInactiveAccounts;

    [ObservableProperty]
    private decimal generalBalance;

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var user = await userSessionService.GetCurrentUserAsync();
            if (user is null) return;

            var all = await accountService.GetAllAsync(user.Id);

            // Calcular saldo real de cada conta via transações (em paralelo)
            var balanceTasks = all.Select(async account =>
            {
                account.CurrentBalance = await transactionService.GetBalanceAsync(account.Id) ?? 0;
            });
            await Task.WhenAll(balanceTasks);

            ActiveAccounts = all.Where(a => a.IsActive).OrderBy(a => a.Name).ToList();
            InactiveAccounts = all.Where(a => !a.IsActive).OrderBy(a => a.Name).ToList();
            HasInactiveAccounts = InactiveAccounts.Count > 0;

            GeneralBalance = ActiveAccounts
                .Where(a => a.IncludeInGeneralBalance)
                .Sum(a => a.CurrentBalance);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddAccount()
    {
        await Shell.Current.GoToAsync(nameof(AccountEditPage));
    }

    [RelayCommand]
    private async Task EditAccount(AccountDTO account)
    {
        await Shell.Current.GoToAsync($"{nameof(AccountEditPage)}?accountId={account.Id}");
    }
}
