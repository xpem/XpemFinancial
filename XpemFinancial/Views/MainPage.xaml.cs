using Service;
using Service.Account;
using Service.Transaction;
using XpemFinancial.VMs;

namespace XpemFinancial.Views
{
    public partial class MainPage : ContentPage
    {
        public MainPage(IAccountService accountService, ITransactionService transactionService,IUserSessionService userSessionService)
        {
            InitializeComponent();

            BindingContext = new VMs.MainVM(accountService, transactionService, userSessionService);
        }

        private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
        {
            CheckBoxIncludePreviousBalance.IsChecked = !CheckBoxIncludePreviousBalance.IsChecked;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await ((MainVM)BindingContext).InitializeAsync();
        }
    }
}
