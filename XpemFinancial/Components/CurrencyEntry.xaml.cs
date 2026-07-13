using System.Globalization;
using System.Text.RegularExpressions;

namespace XpemFinancial.Components;

public partial class CurrencyEntry : ContentView
{
    private bool _isUpdating;
    private readonly CultureInfo _culture = new("pt-BR");

    public CurrencyEntry()
    {
        InitializeComponent();
    }

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(CurrencyEntry),
        string.Empty,
        BindingMode.TwoWay,
        propertyChanged: OnExternalTextChanged);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

#if ANDROID
        if (EntryCurrency?.Handler is Microsoft.Maui.Handlers.EntryHandler handler &&
            handler.PlatformView is AndroidX.AppCompat.Widget.AppCompatEditText editText)
        {
            editText.EmojiCompatEnabled = false;
        }
#endif
    }

    private static void OnExternalTextChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (CurrencyEntry)bindable;
        if (control.EntryCurrency == null || control._isUpdating) return;

        string novoTexto = newValue?.ToString() ?? "";
        if (control.EntryCurrency.Text != novoTexto)
            control.EntryCurrency.Text = novoTexto;
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

    [GeneratedRegex("\\D")]
    private static partial Regex OnlyDigits();

    private void MoveCursorToEnd()
    {
#if ANDROID
        if (EntryCurrency?.Handler is Microsoft.Maui.Handlers.EntryHandler handler &&
            handler.PlatformView is AndroidX.AppCompat.Widget.AppCompatEditText editText)
        {
            editText.Post(() =>
            {
                int pos = editText.Text?.Length ?? 0;
                editText.SetSelection(pos);
            });
            return;
        }
#endif
        // iOS e Windows: adia via Dispatcher do controle para executar
        // após o ciclo nativo de TextChanged finalizar
        EntryCurrency?.Dispatcher.Dispatch(() =>
        {
            if (EntryCurrency?.Text is string t)
                EntryCurrency.CursorPosition = t.Length;
        });
    }

    private void EntryCurrency_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating || EntryCurrency == null) return;

        string digits = OnlyDigits().Replace(e.NewTextValue ?? "", "");
        decimal value = string.IsNullOrEmpty(digits) ? 0m : Convert.ToDecimal(digits);
        string formatted = value <= 0 ? "0,00" : (value / 100m).ToString("N2", _culture);

        if (e.NewTextValue == formatted)
        {
            MoveCursorToEnd();
            return;
        }

        _isUpdating = true;
        try
        {
            EntryCurrency.Text = formatted;
            Text = formatted;
            MoveCursorToEnd();
        }
        finally
        {
            _isUpdating = false;
        }
    }
}