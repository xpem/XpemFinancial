using Microsoft.Extensions.DependencyInjection;
using Service;
using XpemFinancial.VMs;

namespace XpemFinancial
{
    public partial class App : Application
    {
        public IUserService UserService { get; set; }

        public App(IUserService userService, IBuildDbService buildDbService)
        {
            InitializeComponent();

            UserService = userService;

            Application.Current.UserAppTheme = AppTheme.Dark;

            Task.Run(async () => await buildDbService.InitAsync()).Wait();

            // Inicializa o banco local com o mock user ao iniciar o app
            Task.Run(async () => await userService.GetMockUserAsync()).Wait();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var appShellVM = new AppShellVM(UserService);

            _= appShellVM.UserFlyoutAsync();

            return new Window(new AppShell(appShellVM));
        }
    }
}