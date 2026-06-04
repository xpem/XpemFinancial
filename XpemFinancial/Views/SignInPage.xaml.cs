using XpemFinancial.VMs;

namespace XpemFinancial.Views;

public partial class SignInPage : ContentPage
{
    public SignInPage(SignInVM signInVM)
    {
        InitializeComponent();
        BindingContext = signInVM;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        CrashLogLabel.IsVisible = File.Exists(App.CrashLogPath);
    }

    private async void OnViewCrashLogTapped(object sender, TappedEventArgs e)
    {
        if (!File.Exists(App.CrashLogPath))
        {
            CrashLogLabel.IsVisible = false;
            return;
        }

        string log = await File.ReadAllTextAsync(App.CrashLogPath);

        bool clear = await DisplayAlert(
            "Log de erros",
            log.Length > 2000 ? "..." + log[^2000..] : log,
            "Limpar log",
            "Fechar");

        if (clear)
        {
            File.Delete(App.CrashLogPath);
            CrashLogLabel.IsVisible = false;
        }
    }
}
