using Microsoft.Extensions.Logging;
using XpemFinancial.VMs;

namespace XpemFinancial
{
    public partial class AppShell : Shell
    {
        private readonly ILogger<AppShell> _logger;

        public AppShell(AppShellVM appShellVM, ILogger<AppShell> logger)
        {
            _logger = logger;

            InitializeComponent();

            BindingContext = appShellVM;

            if (this.FlyoutHeader is BindableObject flyoutHeader)
            {
                flyoutHeader.BindingContext = appShellVM;
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            ((AppShellVM)BindingContext).Init();
        }

        protected override void OnNavigating(ShellNavigatingEventArgs args)
        {
            base.OnNavigating(args);
        }

        protected override async void OnNavigated(ShellNavigatedEventArgs args)
        {
            try
            {
                base.OnNavigated(args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao navegar para {Target}. Causa provável: recurso de estilo ausente ou XamlParseException.", args.Current?.Location);
                await DisplayAlert("Erro de navegação", $"Não foi possível abrir a tela.\n\n{ex.GetBaseException().Message}", "OK");
            }
        }
    }
}
