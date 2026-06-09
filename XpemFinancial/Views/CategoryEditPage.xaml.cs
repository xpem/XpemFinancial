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

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await _viewModel.InitializeAsync();
    }
}
