using Model.DTO;

namespace XpemFinancial.Components;

public partial class AccountItemView : ContentView
{
    public AccountItemView()
    {
        InitializeComponent();
    }

    public static readonly BindableProperty AccountNameProperty = BindableProperty.Create(
        nameof(AccountName), typeof(string), typeof(AccountItemView), string.Empty);

    public string AccountName
    {
        get => (string)GetValue(AccountNameProperty);
        set => SetValue(AccountNameProperty, value);
    }

    public static readonly BindableProperty AccountBalanceProperty = BindableProperty.Create(
        nameof(AccountBalance), typeof(decimal), typeof(AccountItemView), 0m);

    public decimal AccountBalance
    {
        get => (decimal)GetValue(AccountBalanceProperty);
        set => SetValue(AccountBalanceProperty, value);
    }

    public static readonly BindableProperty AccountTypeProperty = BindableProperty.Create(
        nameof(AccountType), typeof(AccountType), typeof(AccountItemView), default(AccountType));

    public AccountType AccountType
    {
        get => (AccountType)GetValue(AccountTypeProperty);
        set => SetValue(AccountTypeProperty, value);
    }

    public static readonly BindableProperty IsInactiveProperty = BindableProperty.Create(
        nameof(IsInactive), typeof(bool), typeof(AccountItemView), false);

    public bool IsInactive
    {
        get => (bool)GetValue(IsInactiveProperty);
        set => SetValue(IsInactiveProperty, value);
    }
}
