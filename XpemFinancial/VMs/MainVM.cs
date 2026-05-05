using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Service;
using Model.DTO;

namespace XpemFinancial.VMs
{
    public partial class MainVM(IAccountService accountService, ITransactionService transactionService) : VMBase
    {
        [ObservableProperty] private ObservableCollection<TransactionDTO> transactions;
        [ObservableProperty] private TransactionDTO selectedTransaction;
        [ObservableProperty] private bool includePreviousBalance;
        [ObservableProperty] private decimal previousBalance;
        [ObservableProperty] private decimal income;
        [ObservableProperty] private decimal expense;
        [ObservableProperty] private decimal total;
        [ObservableProperty] private bool isNullAccount = false;
        [ObservableProperty] private bool isNotNullAccount = false;
        [ObservableProperty] private string monthYearDisplay;

        private DateTime SelectedDate { get; set; }

        partial void OnIsNullAccountChanged(bool value) => IsNotNullAccount = !value;

        partial void OnSelectedTransactionChanged(TransactionDTO? oldValue, TransactionDTO? newValue)
        {
            if (newValue == null)
                return;

            GoToTransactionEditCommand.Execute(newValue.Id);
        }

        public async Task InitializeAsync()
        {
            MonthYearDisplay = DateTime.Now.ToString("MMMM/yyyy");
            SelectedDate = DateTime.Now;

            var existingAccount = await accountService.GetAsync();
            IsNullAccount = existingAccount == null;
            IsNotNullAccount = !IsNullAccount;
            IncludePreviousBalance = true;
            PreviousBalance = await transactionService.GetPreviousBalanceAsync(SelectedDate);
            Transactions = [];

            var transactionsFromService = await transactionService.GetByMonthYear(DateTime.Now);

            foreach (var transaction in transactionsFromService)
            {
                if (transaction.Type == TransactionType.Income)
                    Income += transaction.Amount;
                else
                    Expense += transaction.Amount;

                Transactions.Add(transaction);
            }

            Total = PreviousBalance + Income - Expense;
        }

        [RelayCommand]
        private async Task GoToAccountPage() => await Shell.Current.GoToAsync($"{nameof(Views.AccountPage)}");

        [RelayCommand]
        private async Task ToggleIncludePreviousBalance()
        {
            IncludePreviousBalance = !IncludePreviousBalance;
            Total = IncludePreviousBalance ? PreviousBalance + Income - Expense : Income - Expense;
            OnPropertyChanged(nameof(Totals));
        }

        [RelayCommand]
        private async Task GoToTransactionEdit(int? transactionId = null)
        {
            var route = transactionId is not null
                ? $"{nameof(Views.TransactionEdit)}?TransactionId={transactionId}"
                : nameof(Views.TransactionEdit);

            await Shell.Current.GoToAsync(route);
        }
    }
}
