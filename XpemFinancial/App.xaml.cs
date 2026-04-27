using Microsoft.Extensions.DependencyInjection;

namespace XpemFinancial
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Força o tema escuro como padrão
            Application.Current.UserAppTheme = AppTheme.Dark;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}