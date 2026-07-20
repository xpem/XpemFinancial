using XpemFinancial.Resources;
using XpemFinancial.VMs;

namespace XpemFinancial.Views;

public partial class CategoryEditPage : ContentPage
{
    private readonly CategoryEditVM _viewModel;

    public CategoryEditPage(CategoryEditVM viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_viewModel.IsEditMode)
        {
            ToolbarItems.Clear();
            ToolbarItems.Add(new ToolbarItem
            {
                IconImageSource = new FontImageSource
                {
                    FontFamily = "Icons",
                    Glyph = IconFont.Ban,
                    Color = Colors.White,
                    Size = 20,
                },
                Command = _viewModel.ToggleActiveCommand,
            });
        }
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await _viewModel.InitializeAsync();
    }
}
