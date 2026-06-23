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
            BindingContext = _vm;
        }

        private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
        {
            CheckBoxIncludePreviousBalance.IsChecked = !CheckBoxIncludePreviousBalance.IsChecked;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.InitializeAsync();
        }
    }
}
