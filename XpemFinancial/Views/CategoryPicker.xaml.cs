using XpemFinancial.VMs;

namespace XpemFinancial.Views;

public partial class CategoryPicker : ContentPage
{
    public CategoryPicker(CategoryPickerVM viewModel)
	{
		InitializeComponent();
        BindingContext = viewModel;
    }
}