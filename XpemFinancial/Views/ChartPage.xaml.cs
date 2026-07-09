using Service;
using Service.Account;
using Service.Recurring;
using Service.Transaction;
using XpemFinancial.Utils;
using XpemFinancial.VMs;

namespace XpemFinancial.Views
{
    public partial class ChartPage : ContentPage
    {
        private readonly ChartVM _vm;
        private readonly LineChartDrawable _drawable = new();

        public ChartPage(ITransactionService transactionService,
            IRecurringRuleService recurringRuleService,
            IUserSessionService userSessionService,
            IUserService userService,
            IAccountService accountService)
        {
            InitializeComponent();

            _vm = new ChartVM(transactionService, recurringRuleService, userSessionService, userService, accountService);
            BindingContext = _vm;

            // Wire the drawable to the GraphicsView
            ChartCanvas.Drawable = _drawable;

            // Whenever the VM finishes loading, push new data into the drawable and redraw
            _vm.DataChanged += OnDataChanged;
        }

        private void OnDataChanged()
        {
            _drawable.IncomePoints = _vm.IncomePoints;
            _drawable.ExpensePoints = _vm.ExpensePoints;
            _drawable.XAxisPointCount = _vm.XAxisPointCount;
            _drawable.XAxisLabels = _vm.XAxisLabels;
            _drawable.MaxValue = _vm.MaxValue;

            // Invalidate must happen on the UI thread
            MainThread.BeginInvokeOnMainThread(() => ChartCanvas.Invalidate());
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.InitializeAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _vm.DataChanged -= OnDataChanged;
        }
    }
}
