namespace XpemFinancial.Views;

public partial class TransactionEdit : ContentPage
{
	public TransactionEdit()
	{
		InitializeComponent();

		BindingContext = new VMs.TransactionEditVM();
    }
}