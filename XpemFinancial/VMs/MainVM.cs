using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Service;

namespace XpemFinancial.VMs
{
    public partial class MainVM(IAccountService accountService) : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<Transaction> transactions;

        [ObservableProperty]
        private bool includePreviousBalance;

        [ObservableProperty]
        private decimal previousBalance;

        [ObservableProperty]
        private decimal income;

        [ObservableProperty]
        private decimal expense;

        [ObservableProperty]
        private decimal total;

        [ObservableProperty]
        private bool isNullAccount = false;

        public async Task InitializeAsync()
        {
            var existingAccount = await accountService.GetAsync();

            if (existingAccount == null)
            {
                IsNullAccount = true;
            }
            else
                IsNullAccount = false;

            IncludePreviousBalance = true;

            //saldo anterior será o saldo da conta menos as transações do mes até o dia de hoje.
            PreviousBalance = existingAccount?.Balance ?? 0;

            Income = 0;
            Expense = 0;

            //saldo anterior será o saldo da conta mais  as transações do mes incluindo as futuras.
            Total = PreviousBalance + Income - Expense;

            // Criando a lista de Mock
            Transactions = [];
        }

        [RelayCommand]
        private async Task GoToAccountPage() => await Shell.Current.GoToAsync($"{nameof(Views.AccountPage)}");

        [RelayCommand]
        private async Task ToggleIncludePreviousBalance()
        {
            // 1. Toggle the state
            IncludePreviousBalance = !IncludePreviousBalance;

            // 2. Perform the conditional calculation
            if (IncludePreviousBalance)
            {
                Total = PreviousBalance + Income - Expense;
            }
            else
            {
                Total = Income - Expense;
            }

            // 3. Force the UI to refresh the Totals object
            // Note: It's better if the Totals class inherits from ObservableObject,
            // but this manual trigger works if Totals is a POCO.
            OnPropertyChanged(nameof(Totals));
        }

        [RelayCommand]
        private async Task GoToTransactionEdit() => await Shell.Current.GoToAsync($"{nameof(Views.TransactionEdit)}");

    }
}
