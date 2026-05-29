using XpemFinancial.VMs;

namespace XpemFinancial.Views;

public partial class TransactionEdit : ContentPage
{
    private bool _initialized = false;

    public TransactionEdit(VMs.TransactionEditVM transactionEditVM)
    {
        InitializeComponent();
        BindingContext = transactionEditVM;
    }

    //protected override async void OnAppearing()
    //{
    //    base.OnAppearing();
    //    if (!_initialized)
    //    {
    //        _initialized = true;
    //        await ((TransactionEditVM)BindingContext).InitializeAsync();
    //    }
    //}
}