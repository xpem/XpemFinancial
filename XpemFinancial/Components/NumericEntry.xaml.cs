using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace XpemFinancial.Components;

public partial class NumericEntry : ContentView
{
    private bool _isUpdating;

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(int),
        typeof(NumericEntry),
        0,
        BindingMode.TwoWay,
        propertyChanged: OnExternalValueChanged);

    public int Text
    {
        get => (int)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private static void OnExternalValueChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (NumericEntry)bindable;
        if (control.EntryNumeric == null || control._isUpdating) return;

        string text = newValue is int i ? i.ToString() : "0";  // ✅ trata como int
        if (control.EntryNumeric.Text != text)
            control.EntryNumeric.Text = text;
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex OnlyDigits();

    private void MoveCursorToEnd()
    {
#if ANDROID
        if (EntryNumeric?.Handler is Microsoft.Maui.Handlers.EntryHandler handler &&
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
        EntryNumeric?.Dispatcher.Dispatch(() =>
        {
            if (EntryNumeric?.Text is string t)
                EntryNumeric.CursorPosition = t.Length;
        });
    }

    private void EntryNumeric_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating || EntryNumeric == null) return;

        string digits = OnlyDigits().Replace(e.NewTextValue ?? "", "");

        int parsed = string.IsNullOrEmpty(digits) ? 0 : (int.TryParse(digits, out int p) ? p : 0);
        string normalized = string.IsNullOrEmpty(digits) ? "" : parsed.ToString();

        _isUpdating = true;
        try
        {
            // Propaga ao ViewModel — _isUpdating bloqueia OnExternalValueChanged
            // de tocar no EntryNumeric.Text enquanto estamos dentro do TextChanged
            SetValue(TextProperty, parsed);

            // Só reescreve o texto se for necessário normalizar (ex: "007" → "7")
            // Não reescreve se o campo está vazio — evita SetSelection em posição inválida
            if (!string.IsNullOrEmpty(normalized) && EntryNumeric.Text != normalized)
                EntryNumeric.Text = normalized; // vai disparar novo TextChanged que chama MoveCursorToEnd
            else if (!string.IsNullOrEmpty(normalized))
                MoveCursorToEnd();
            // campo vazio: não chama MoveCursorToEnd — cursor já está em 0 naturalmente
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