using Microsoft.Extensions.DependencyInjection;
using Service;

namespace XpemFinancial
{
    public partial class App : Application
    {
        public App(IUserService userService, IBuildDbService buildDbService)
        {
            InitializeComponent();

            Application.Current.UserAppTheme = AppTheme.Dark;

            buildDbService.Init();

            // Inicializa o banco local com o mock user ao iniciar o app
            Task.Run(async () => await userService.GetMockUserAsync());
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}