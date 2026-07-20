using XpemFinancial.Resources;
using XpemFinancial.VMs;

namespace XpemFinancial.Views;

public partial class TransactionEditPage : ContentPage
{
    public TransactionEditPage(VMs.TransactionEditVM transactionEditVM)
    {
        InitializeComponent();
        BindingContext = transactionEditVM;
        transactionEditVM.PropertyChanged += OnVMPropertyChanged;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await ((TransactionEditVM)BindingContext).InitializeAccountsAsync();
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

    private void OnDescriptionEntrySizeChanged(object sender, EventArgs e)
    {
        PositionSuggestionList();
    }

    // Reposition whenever the suggestion list becomes visible so the scroll offset is current.
    private void OnVMPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TransactionEditVM.SuggestionsVisible))
            PositionSuggestionList();
    }

    private void PositionSuggestionList()
    {
        // Alternative approach using Bounds property
        var rootGrid = ListaSugestoes.Parent as VisualElement;
        if (rootGrid is null)
            return;

        // Use the Y position from the Bounds property
        var locationY = DescriptionEntry.Y;

        // Place the list immediately below the DescriptionEntry,
        // matching the ScrollView's horizontal margin (10).
        ListaSugestoes.Margin = new Thickness(10, locationY + DescriptionEntry.Height, 10, 0);
    }
}