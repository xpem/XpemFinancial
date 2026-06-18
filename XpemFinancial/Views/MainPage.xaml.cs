using Service;
using Service.Account;
using Service.Recurring;
using Service.Transaction;
using XpemFinancial.Utils.Services;
using XpemFinancial.VMs;

namespace XpemFinancial.Views
{
    public partial class MainPage : ContentPage
    {
        private readonly MainVM _vm;
        private readonly SyncService _syncService;

        public MainPage(
            IAccountService accountService,
            ITransactionService transactionService,
            IUserSessionService userSessionService,
            IRecurringRuleService recurringRuleService,
            IUserService userService,
            SyncService syncService)
        {
            InitializeComponent();

            _vm = new MainVM(accountService, transactionService, userSessionService, recurringRuleService, userService);
            _syncService = syncService;
            BindingContext = _vm;
        }

        private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
        {
            CheckBoxIncludePreviousBalance.IsChecked = !CheckBoxIncludePreviousBalance.IsChecked;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Fix #4: subscribe when page is visible so background sync refreshes the list.
            _syncService.SyncCompleted += OnSyncCompleted;

            await _vm.InitializeAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _syncService.SyncCompleted -= OnSyncCompleted;
        }

        // Called from a thread-pool thread — marshal to UI thread before touching the VM.
        private void OnSyncCompleted()
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                // Only refresh silently if the VM is already initialised (avoids double-load
                // on the very first appearance before InitializeAsync has run).
                if (_vm.MonthYearDisplay is not null)
                    await _vm.RefreshTransactionsAsync();
            });
        }
    }
}
