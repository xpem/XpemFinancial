using XpemFinancial.VMs;

namespace XpemFinancial.Views;

public partial class CategoryManagementPage : ContentPage
{
    private readonly CategoryManagementVM _viewModel;

    public CategoryManagementPage(CategoryManagementVM viewModel)
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
