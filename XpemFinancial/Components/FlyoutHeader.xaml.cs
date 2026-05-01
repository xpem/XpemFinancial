namespace XpemFinancial.Components;

public partial class FlyoutHeader : ContentView
{
	public FlyoutHeader()
	{
		InitializeComponent();
	}

    public static readonly BindableProperty EmailProperty = BindableProperty.Create(
       propertyName: nameof(Email), returnType: typeof(string), defaultValue: null, defaultBindingMode: BindingMode.OneWay, declaringType: typeof(ContentView));

    public string Email { get => (string)GetValue(EmailProperty); set { SetValue(EmailProperty, value); } }

    public static readonly BindableProperty NameProperty = BindableProperty.Create(
        propertyName: nameof(Name), returnType: typeof(string), defaultValue: null, defaultBindingMode: BindingMode.OneWay, declaringType: typeof(ContentView));

    public string Name { get => (string)GetValue(NameProperty); set { SetValue(NameProperty, value); } }
}