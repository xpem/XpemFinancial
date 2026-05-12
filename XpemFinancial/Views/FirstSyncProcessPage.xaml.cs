using XpemFinancial.VMs;

namespace XpemFinancial.Views;

public partial class FirstSyncProcessPage : ContentPage
{
	public FirstSyncProcessPage(FirstSyncProcessVM firstSyncProcessVM)
	{
		InitializeComponent();
		BindingContext = firstSyncProcessVM;
	}
}