using XpemFinancial.VMs;

namespace XpemFinancial.Views;

public partial class FirstSyncProcessPage : ContentPage
{
	public FirstSyncProcessPage(FirstSyncProcessVM firstSyncProcessVM)
	{
		InitializeComponent();
		BindingContext = firstSyncProcessVM;
	}

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((FirstSyncProcessVM)BindingContext).SyncProcess();
    }
}