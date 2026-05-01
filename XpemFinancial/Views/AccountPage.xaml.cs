using XpemFinancial.VMs;

namespace XpemFinancial.Views;

public partial class AccountPage : ContentPage
{
	public AccountPage(AccountVM accountVM)
	{
		InitializeComponent();

		BindingContext = accountVM;
	}
}