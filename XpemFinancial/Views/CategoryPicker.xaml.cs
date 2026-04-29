using XpemFinancial.VMs;

namespace XpemFinancial.Views;

public partial class CategoryPicker : ContentPage
{
    private readonly CategoryPickerVM _viewModel;

    public CategoryPicker(CategoryPickerVM viewModel)
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

    protected override bool OnBackButtonPressed()
    {
        // Volta sem item selecionado
        Shell.Current.GoToAsync("..");
        return true; // true para não deixar o sistema processar o evento novamente
    }
}