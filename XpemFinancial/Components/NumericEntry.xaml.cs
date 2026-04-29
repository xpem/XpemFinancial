using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace XpemFinancial.Components;

public partial class NumericEntry : ContentView
{
    private bool _isUpdating;

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(NumericEntry),
        string.Empty,
        BindingMode.TwoWay,
        propertyChanged: OnExternalValueChanged);

    public int Text
    {
        get => (int)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }


    public static readonly BindableProperty LabelTextProperty = BindableProperty.Create(
        nameof(LabelText), typeof(string), typeof(CurrencyEntry), string.Empty);

    public string LabelText
    {
        get => (string)GetValue(LabelTextProperty);
        set => SetValue(LabelTextProperty, value);
    }

    private static void OnExternalValueChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (NumericEntry)bindable;
        if (control.EntryNumeric == null || control._isUpdating) return;

        string text = newValue?.ToString() ?? "0";
        if (control.EntryNumeric.Text != text)
            control.EntryNumeric.Text = text;
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex OnlyDigits();

    private void EntryNumeric_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating || EntryNumeric == null) return;

        string digits = OnlyDigits().Replace(e.NewTextValue ?? "", "");

        if (string.IsNullOrEmpty(digits)) digits = "0";

        // Remove zeros à esquerda
        digits = int.TryParse(digits, out int parsed) ? parsed.ToString() : "0";

        if (e.NewTextValue == digits)
            return;

        _isUpdating = true;
        try
        {
            EntryNumeric.Text = digits;
            Text = int.Parse(digits);
            EntryNumeric.Dispatcher.Dispatch(() =>
            {
                if (EntryNumeric?.Text is string t)
                    EntryNumeric.CursorPosition = t.Length;
            });
        }
        finally
        {
            _isUpdating = false;
        }
    }

    public NumericEntry()
	{
		InitializeComponent();
	}
}