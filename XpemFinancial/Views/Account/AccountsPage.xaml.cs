using XpemFinancial.VMs;

namespace XpemFinancial.Views.Account;

public partial class AccountsPage : ContentPage
{
    private readonly AccountsVM _viewModel;

    public AccountsPage(AccountsVM viewModel)
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
