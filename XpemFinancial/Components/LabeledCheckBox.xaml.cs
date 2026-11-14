namespace XpemFinancial.Components;

public partial class LabeledCheckBox : ContentView
{
    public LabeledCheckBox()
    {
        InitializeComponent();
    }

    private void OnLabelTapped(object? sender, TappedEventArgs e)
    {
        IsChecked = !IsChecked;
    }

    public static readonly BindableProperty IsCheckedProperty = BindableProperty.Create(
        propertyName: nameof(IsChecked),
        returnType: typeof(bool),
        declaringType: typeof(LabeledCheckBox),
        defaultValue: false,
        defaultBindingMode: BindingMode.TwoWay);

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        propertyName: nameof(Text),
        returnType: typeof(string),
        declaringType: typeof(LabeledCheckBox),
        defaultValue: null,
        defaultBindingMode: BindingMode.OneWay);

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly BindableProperty ColorProperty = BindableProperty.Create(
        propertyName: nameof(Color),
        returnType: typeof(Color),
        declaringType: typeof(LabeledCheckBox),
        defaultValue: Colors.White,
        defaultBindingMode: BindingMode.OneWay);

    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(
        propertyName: nameof(FontAttributes),
        returnType: typeof(FontAttributes),
        declaringType: typeof(LabeledCheckBox),
        defaultValue: FontAttributes.None,
        defaultBindingMode: BindingMode.OneWay);

    public FontAttributes FontAttributes
    {
        get => (FontAttributes)GetValue(FontAttributesProperty);
        set => SetValue(FontAttributesProperty, value);
    }
}
