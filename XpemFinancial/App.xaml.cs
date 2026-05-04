using Microsoft.Extensions.DependencyInjection;
using Service;
using XpemFinancial.VMs;

namespace XpemFinancial
{
    public partial class App : Application
    {
        public IUserSessionService UserSessionService { get; set; }

        public App(IUserService userService, IUserSessionService userSessionService, ICategoryService categoryService,
            IBuildDbService buildDbService, IAccountService accountService)
        {
            InitializeComponent();

            UserSessionService = userSessionService;

            Application.Current.UserAppTheme = AppTheme.Dark;

            Task.Run(async () => await buildDbService.InitAsync()).Wait();

            // Inicializa o banco local com o mock user ao iniciar o app
            Task.Run(async () => await userService.GetMockUserAsync()).Wait();

            Task.Run(async () => await accountService.MockAccount(1)).Wait();

            Task.Run(async () => await categoryService.MockCategories(1)).Wait();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var appShellVM = new AppShellVM(UserSessionService);

            _ = appShellVM.UserFlyoutAsync();

            return new Window(new AppShell(appShellVM));
        }
    }
}