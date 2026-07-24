using XpemFinancial.Resources;

namespace XpemFinancial.Components;

public partial class RequiredEntry : ContentView
{
    private bool _isPasswordVisible;

    public RequiredEntry()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Propriedade computada que controla o estado real de IsPassword no Entry interno.
    /// Quando IsPassword é true e o usuário não togglou, retorna true (senha oculta).
    /// </summary>
    public bool IsPasswordActive => IsPassword && !_isPasswordVisible;

    private void OnTogglePasswordVisibility(object sender, EventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;
        OnPropertyChanged(nameof(IsPasswordActive));
        TogglePasswordIcon.Text = _isPasswordVisible ? IconFont.EyeSlash : IconFont.Eye;
    }

    public static readonly BindableProperty LabelTextProperty = BindableProperty.Create(
        propertyName: nameof(LabelText),
        returnType: typeof(string),
        declaringType: typeof(RequiredEntry),
        defaultValue: null,
        defaultBindingMode: BindingMode.OneWay);

    public string LabelText
    {
        get => (string)GetValue(LabelTextProperty);
        set => SetValue(LabelTextProperty, value);
    }

    public static readonly BindableProperty EntryValueProperty = BindableProperty.Create(
        propertyName: nameof(EntryValue),
        returnType: typeof(string),
        declaringType: typeof(RequiredEntry),
        defaultValue: null,
        defaultBindingMode: BindingMode.TwoWay);

    public string EntryValue
    {
        get => (string)GetValue(EntryValueProperty);
        set => SetValue(EntryValueProperty, value);
    }

    public static readonly BindableProperty IsRequiredProperty = BindableProperty.Create(
        propertyName: nameof(IsRequired),
        returnType: typeof(bool),
        declaringType: typeof(RequiredEntry),
        defaultValue: false,
        defaultBindingMode: BindingMode.OneWay);

    public bool IsRequired
    {
        get => (bool)GetValue(IsRequiredProperty);
        set => SetValue(IsRequiredProperty, value);
    }

    public static readonly BindableProperty RequiredTextProperty = BindableProperty.Create(
        propertyName: nameof(RequiredText),
        returnType: typeof(string),
        declaringType: typeof(RequiredEntry),
        defaultValue: "Campo obrigatório",
        defaultBindingMode: BindingMode.OneWay);

    public string RequiredText
    {
        get => (string)GetValue(RequiredTextProperty);
        set => SetValue(RequiredTextProperty, value);
    }

    public static readonly BindableProperty IsPasswordProperty = BindableProperty.Create(
        propertyName: nameof(IsPassword),
        returnType: typeof(bool),
        declaringType: typeof(RequiredEntry),
        defaultValue: false,
        defaultBindingMode: BindingMode.OneWay,
        propertyChanged: (bindable, _, _) =>
        {
            var control = (RequiredEntry)bindable;
            control._isPasswordVisible = false;
            control.OnPropertyChanged(nameof(IsPasswordActive));
        });

    public bool IsPassword
    {
        get => (bool)GetValue(IsPasswordProperty);
        set => SetValue(IsPasswordProperty, value);
    }

    public static readonly BindableProperty IsTextPredictionEnabledProperty = BindableProperty.Create(
        propertyName: nameof(IsTextPredictionEnabled),
        returnType: typeof(bool),
        declaringType: typeof(RequiredEntry),
        defaultValue: true,
        defaultBindingMode: BindingMode.OneWay);

    public bool IsTextPredictionEnabled
    {
        get => (bool)GetValue(IsTextPredictionEnabledProperty);
        set => SetValue(IsTextPredictionEnabledProperty, value);
    }

    public static readonly BindableProperty MaxLengthProperty = BindableProperty.Create(
        propertyName: nameof(MaxLength),
        returnType: typeof(int),
        declaringType: typeof(RequiredEntry),
        defaultValue: int.MaxValue,
        defaultBindingMode: BindingMode.OneWay);

    public int MaxLength
    {
        get => (int)GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }
}