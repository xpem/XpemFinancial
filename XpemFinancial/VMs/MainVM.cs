using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.DTO;
using Service;
using Service.Account;
using Service.Recurring;
using Service.Transaction;
using System.Collections.ObjectModel;
using XpemFinancial.Views;

namespace XpemFinancial.VMs
{
    public partial class MainVM(IAccountService accountService,
        ITransactionService transactionService,
        IUserSessionService userSessionService,
        IRecurringRuleService recurringRuleService,
        IUserService userService) : VMBase
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
        [ObservableProperty] private bool isRequired;

        private DateTime SelectedDate { get; set; }
        private int? _currentUserId;

        partial void OnIncludePreviousBalanceChanged(bool value)
        {
            Total = value
                ? (PreviousBalance + Income) + Expense
                : Income + Expense;

            // persiste a preferência de forma assíncrona (fire-and-forget intencional)
            if (_currentUserId.HasValue)
                _ = userService.UpdateIncludePreviousBalanceAsync(value, _currentUserId.Value);
        }

        partial void OnIsNullAccountChanged(bool value) => IsNotNullAccount = !value;

        partial void OnSelectedTransactionChanged(TransactionDTO? oldValue, TransactionDTO? newValue)
        {
            if (newValue == null)
                return;

            GoToTransactionEditCommand.Execute(newValue.Id);
        }

        public async Task InitializeAsync()
        {
            var user = await userSessionService.GetCurrentUserAsync();

            if (user == null)
            {
                _ = Shell.Current.GoToAsync($"{nameof(SignInPage)}");
                return;
            }

            SelectedDate = DateTime.Now;
            _currentUserId = user.Id;
            IncludePreviousBalance = user.IncludePreviousBalance;

            var existingAccount = await accountService.GetAsync();
            IsNullAccount = existingAccount == null;
            IsNotNullAccount = !IsNullAccount;

            await LoadTransactionsForMonthAsync(SelectedDate);
        }

        private async Task LoadTransactionsForMonthAsync(DateTime date)
        {
            MonthYearDisplay = date.ToString("MMMM/yyyy");
            PreviousBalance = await transactionService.GetPreviousBalanceAsync(date);
            Transactions = [];
            Expense = Income = 0;

            var transactionsFromService = await transactionService.GetByMonthYear(date);

            foreach (var transaction in transactionsFromService)
            {
                if (transaction.Type == TransactionType.Income)
                    Income += transaction.Amount;
                else
                    Expense += transaction.Amount;

                Transactions.Add(transaction);
            }

            Total = IncludePreviousBalance
                ? (PreviousBalance + Income) + Expense
                : Income + Expense;
        }

        [RelayCommand]
        private async Task LoadPreviousMonth()
        {
            SelectedDate = SelectedDate.AddMonths(-1);
            await LoadTransactionsForMonthAsync(SelectedDate);
        }

        [RelayCommand]
        private async Task LoadNextMonth()
        {
            SelectedDate = SelectedDate.AddMonths(1);

            if (SelectedDate > DateTime.Today.AddMonths(3))
                await recurringRuleService.RunSchedulerAsync(SelectedDate);

            await LoadTransactionsForMonthAsync(SelectedDate);
        }

        [RelayCommand]
        private async Task GoToAccountPage() => await Shell.Current.GoToAsync($"{nameof(Views.AccountPage)}");

        [RelayCommand]
        private void ToggleIncludePreviousBalance()
        {
            IncludePreviousBalance = !IncludePreviousBalance;
        }

        [RelayCommand]
        private async Task GoToTransactionEdit(int? transactionId = null)
        {
            var route = transactionId is not null
                ? $"{nameof(Views.TransactionEditPage)}?TransactionId={transactionId}"
                : nameof(Views.TransactionEditPage);

            await Shell.Current.GoToAsync(route);
        }
    }
}
