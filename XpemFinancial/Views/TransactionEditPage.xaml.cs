using XpemFinancial.Resources;
using XpemFinancial.VMs;

namespace XpemFinancial.Views;

public partial class TransactionEditPage : ContentPage
{
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

    private async void OnSugestaoSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Model.Res.TransactionDescriptionRes item)
            return;

        var vm = (TransactionEditVM)BindingContext;
        await vm.ApplySuggestion(item);
        ((CollectionView)sender).SelectedItem = null;
    }
}