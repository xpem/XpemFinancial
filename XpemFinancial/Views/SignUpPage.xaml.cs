using XpemFinancial.VMs;

namespace XpemFinancial.Views;

public partial class SignUpPage : ContentPage
{
	public SignUpPage(SignUpVM signUpVM)
	{
		InitializeComponent();

		BindingContext = signUpVM;
    }
}