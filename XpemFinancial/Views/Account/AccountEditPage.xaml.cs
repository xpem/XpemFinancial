using XpemFinancial.Resources;
using XpemFinancial.VMs;

namespace XpemFinancial.Views.Account;

public partial class AccountEditPage : ContentPage
{
    private readonly AccountEditVM _viewModel;

    public AccountEditPage(AccountEditVM viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var vm = (AccountEditVM)BindingContext;
        if (vm.IsEditMode)
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
                Command = vm.DeactivateCommand,
            });
        }
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await _viewModel.InitializeAsync();
    }
}
