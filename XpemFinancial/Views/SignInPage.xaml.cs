using XpemFinancial.VMs;

namespace XpemFinancial.Views;

public partial class SignInPage : ContentPage
{
	public SignInPage(SignInVM signInVM)
	{
		InitializeComponent();
		BindingContext = signInVM;
	}
}