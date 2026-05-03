using XpemFinancial.VMs;

namespace XpemFinancial.Views;

public partial class AccountPage : ContentPage
{
	public AccountPage(AccountVM accountVM)
	{
		InitializeComponent();

		BindingContext = accountVM;
	}

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((AccountVM)BindingContext).InitializeAsync();
    }
}