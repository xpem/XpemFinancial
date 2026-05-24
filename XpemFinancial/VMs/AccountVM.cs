using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.DTO;
using Service;
using Service.Account;
using Service.Transaction;

namespace XpemFinancial.VMs
{
    public partial class AccountVM(IAccountService accountService, IUserSessionService userSessionService, ITransactionService transactionService) : VMBase
    {
        [ObservableProperty]
        private string currentBalance;

        [ObservableProperty]
        private bool isEditingBalance = false;

        [ObservableProperty]
        private bool isNotEditingBalance = true;

        private AccountDTO Account { get; set; }

        private decimal OriginalBalance { get; set; }

        partial void OnIsEditingBalanceChanged(bool value)
        {
            IsNotEditingBalance = !value;
        }

        [RelayCommand]
        private async Task SaveBalance()
        {
            if (CurrentBalance == OriginalBalance.ToString("C"))
            {
                IsEditingBalance = false;
                return;
            }

            decimal newBalance = decimal.Parse(CurrentBalance, System.Globalization.NumberStyles.Currency);

            await accountService.AdjustAccountBalanceAsync(Account, OriginalBalance, newBalance, IsOn);

            if (DeviceInfo.Platform == DevicePlatform.iOS || DeviceInfo.Platform == DevicePlatform.Android)
            {
                ToastDuration duration = ToastDuration.Short;

                var toast = Toast.Make("Valor atualizado!", duration, 15);
                await toast.Show();
            }
            else
                await Application.Current.Windows[0].Page.DisplayAlertAsync("Success", "Valor atualizado!", null, "Ok");

            IsEditingBalance = false;
        }

        [RelayCommand]
        private void CancelEditBalance()
        {
            IsEditingBalance = false;
        }

        [RelayCommand]
        private void EditBalance()
        {
            IsEditingBalance = true;
        }

        public async Task InitializeAsync()
        {
            var existingAccount = await accountService.GetAsync();

            if (existingAccount == null)
            {
                var currentUser = await userSessionService.GetCurrentUserAsync();
                Account = new AccountDTO { UserId = currentUser?.Id ?? 0 };
            }
            else
            {
                Account = existingAccount;
            }

            OriginalBalance = await transactionService.GetBalanceAsync(Account.Id) ?? 0;
            CurrentBalance = OriginalBalance.ToString("C");
        }
    }
}
