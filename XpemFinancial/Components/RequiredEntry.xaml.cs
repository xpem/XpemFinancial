namespace XpemFinancial.Components;

public partial class RequiredEntry : ContentView
{
    public RequiredEntry()
    {
        InitializeComponent();
        RequiredText = "Campo obrigatório";
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
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: OnIsRequiredChanged);

    public bool IsRequired
    {
        get => (bool)GetValue(IsRequiredProperty);
        set => SetValue(IsRequiredProperty, value);
    }

    private static void OnIsRequiredChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (RequiredEntry)bindable;
        control.RequiredStrokeThickness = (bool)newValue ? 1 : 0;
    }

    public static readonly BindableProperty RequiredTextProperty = BindableProperty.Create(
        propertyName: nameof(RequiredText),
        returnType: typeof(string),
        declaringType: typeof(RequiredEntry),
        defaultValue: null,
        defaultBindingMode: BindingMode.TwoWay);

    public string RequiredText
    {
        get => (string)GetValue(RequiredTextProperty);
        set => SetValue(RequiredTextProperty, value);
    }

    public static readonly BindableProperty IsTextPredictionEnabledProperty = BindableProperty.Create(
        propertyName: nameof(IsTextPredictionEnabled),
        returnType: typeof(bool),
        declaringType: typeof(RequiredEntry),
        defaultValue: true,
        defaultBindingMode: BindingMode.TwoWay);

    public bool IsTextPredictionEnabled {
        get => (bool)GetValue(IsTextPredictionEnabledProperty);
        set => SetValue(IsTextPredictionEnabledProperty, value);
    }

    //valor será 1 caso IsRequired seja true, caso contrário, será 0
    public static readonly BindableProperty RequiredStrokeThicknessProperty = BindableProperty.Create(
        propertyName: nameof(RequiredStrokeThickness),
        returnType: typeof(int),
        declaringType: typeof(RequiredEntry),
        defaultValue: 0,
        defaultBindingMode: BindingMode.OneWay);

    public int RequiredStrokeThickness { 
        get => (int)GetValue(RequiredStrokeThicknessProperty);
        set => SetValue(RequiredStrokeThicknessProperty, value);
    }



}