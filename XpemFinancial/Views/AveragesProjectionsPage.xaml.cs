using XpemFinancial.Utils;
using XpemFinancial.VMs;

namespace XpemFinancial.Views
{
    public partial class AveragesProjectionsPage : ContentPage
    {
        private readonly AveragesProjectionsVM _vm;
        private readonly AveragesChartDrawable _drawable = new();

        public AveragesProjectionsPage(AveragesProjectionsVM vm)
        {
            InitializeComponent();

            _vm = vm;
            BindingContext = _vm;

            ChartCanvas.Drawable = _drawable;
            _vm.DataChanged += OnDataChanged;
        }

        private void OnDataChanged()
        {
            _drawable.IncomePoints = _vm.IncomePoints;
            _drawable.ExpensePoints = _vm.ExpensePoints;
            _drawable.XAxisPointCount = _vm.XAxisPointCount;
            _drawable.XAxisLabels = _vm.XAxisLabels;
            _drawable.RealPointCount = _vm.RealPointCount;
            _drawable.MaxValue = _vm.MaxValue;

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
