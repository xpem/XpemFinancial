using XpemFinancial.VMs;

namespace XpemFinancial.Views;

public partial class TransactionEdit : ContentPage
{


    public TransactionEdit(VMs.TransactionEditVM transactionEditVM)
    {
        InitializeComponent();
        BindingContext = transactionEditVM;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((TransactionEditVM)BindingContext).InitializeAsync();
    }
}