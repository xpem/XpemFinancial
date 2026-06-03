using XpemFinancial.VMs;

namespace XpemFinancial.Views;

public partial class UpdatePasswordPage : ContentPage
{
	public UpdatePasswordPage(UpdatePasswordVM updatePasswordVM)
	{
		InitializeComponent();
		BindingContext = updatePasswordVM;
	}
}