using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.DTO;
using Service;
using System;
using System.Collections.Generic;
using System.Text;

namespace XpemFinancial.VMs
{
    public partial class AccountVM(IAccountService accountService, IUserSessionService userSessionService) : VMBase
    {
        [ObservableProperty]
        private string currentBalance;

        [ObservableProperty]
        private bool isEditingBalance = false;

        [ObservableProperty]
        private bool isNotEditingBalance = true;

        private AccountDTO Account { get; set; }

        partial void OnIsEditingBalanceChanged(bool value)
        {
            IsNotEditingBalance = !value;
        }

        [RelayCommand]
        private async Task SaveBalance()
        {
            if (CurrentBalance == Account.Balance.ToString("C"))
            {
                IsEditingBalance = false;
                return;
            }

            Account.Balance = decimal.Parse(CurrentBalance, System.Globalization.NumberStyles.Currency);

            await accountService.AdjustAccountBalanceAsync(Account);

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
                Account = new AccountDTO { Balance = 0, UserId = currentUser?.Id ?? 0 };
            }
            else
            {
                Account = existingAccount;
            }

            CurrentBalance = Account.Balance.ToString("C");
        }
    }
}
