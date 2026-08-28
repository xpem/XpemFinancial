using XpemFinancial.VMs.Transaction;

namespace XpemFinancial.Views.Transaction;

public partial class TransactionListPage : ContentPage
{
    private readonly TransactionListVM _vm;

    public TransactionListPage(TransactionListVM vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitializeAsync();
    }
}
