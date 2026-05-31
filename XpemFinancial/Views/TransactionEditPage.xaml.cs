using XpemFinancial.Resources;
using XpemFinancial.VMs;

namespace XpemFinancial.Views;

public partial class TransactionEditPage : ContentPage
{
    private bool _initialized = false;

    public TransactionEditPage(VMs.TransactionEditVM transactionEditVM)
    {
        InitializeComponent();
        BindingContext = transactionEditVM;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var vm = (TransactionEditVM)BindingContext;
        if (vm.IsEditing)
        {
            ToolbarItems.Clear();
            ToolbarItems.Add(new ToolbarItem
            {
                IconImageSource = new FontImageSource
                {
                    FontFamily = "Icons",
                    Glyph = IconFont.Trash,
                    Color = Colors.White,
                    Size = 20,
                },
                Command = vm.DeleteTransactionCommand,
            });
        }
    }
}