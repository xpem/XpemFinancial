using Microsoft.Extensions.DependencyInjection;
using Service;
using Service.Account;
using Service.Category;
using Service.Recurring;
using XpemFinancial.VMs;

namespace XpemFinancial
{
    public partial class App : Application
    {
        public IUserSessionService UserSessionService { get; set; }

        private readonly IUserService _userService;
        private readonly ICategoryService _categoryService;
        private readonly IBuildDbService _buildDbService;
        private readonly IAccountService _accountService;
        private readonly IRecurringRuleService _recurringRuleService;
        public readonly string Version = "@0.2.5";

        public App(IUserService userService, IUserSessionService userSessionService, ICategoryService categoryService,
            IBuildDbService buildDbService, IAccountService accountService, IRecurringRuleService recurringRuleService)
        {
            InitializeComponent();

            UserSessionService = userSessionService;
            _userService = userService;
            _categoryService = categoryService;
            _buildDbService = buildDbService;
            _accountService = accountService;
            _recurringRuleService = recurringRuleService;

            Application.Current!.UserAppTheme = AppTheme.Dark;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Exibe loading enquanto inicializa
            var loadingPage = new ContentPage
            {
                Content = new ActivityIndicator { IsRunning = true, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
            };

            var window = new Window(loadingPage);

            _ = InitializeAndNavigateAsync(window);

            return window;
        }

        private async Task InitializeAndNavigateAsync(Window window)
        {
            await _buildDbService.InitAsync();
            await _recurringRuleService.RunSchedulerAsync();
            //await _userService.GetMockUserAsync();
            //await _accountService.MockAccount(1);
            //await _categoryService.MockCategories(1);

            var appShellVM = new AppShellVM(UserSessionService, _buildDbService);
            await appShellVM.UserFlyoutAsync();

            // Só navega para o Shell após tudo pronto
            window.Page = new AppShell(appShellVM);
        }
    }
}