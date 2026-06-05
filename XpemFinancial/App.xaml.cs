using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Service;
using Service.Account;
using Service.Category;
using Service.Recurring;
using XpemFinancial.Utils;
using XpemFinancial.VMs;

namespace XpemFinancial
{
    public partial class App : Application
    {
        public static string CrashLogPath { get; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "crash.log");

        public IUserSessionService UserSessionService { get; set; }

        private readonly IUserService _userService;
        private readonly ICategoryService _categoryService;
        private readonly IBuildDbService _buildDbService;
        private readonly IAccountService _accountService;
        private readonly IRecurringRuleService _recurringRuleService;
        private readonly SyncService _syncService;
        private readonly ILogger<AppShell> _appShellLogger;
        public readonly string Version = AppInfo.Current.BuildString;


        public App(IUserService userService, IUserSessionService userSessionService, ICategoryService categoryService,
            IBuildDbService buildDbService, IAccountService accountService, IRecurringRuleService recurringRuleService,
            SyncService syncService, ILogger<AppShell> appShellLogger)
        {
            RegisterCrashHandlers();

            InitializeComponent();

            UserSessionService = userSessionService;
            _userService = userService;
            _categoryService = categoryService;
            _buildDbService = buildDbService;
            _accountService = accountService;
            _recurringRuleService = recurringRuleService;
            _syncService = syncService;
            _appShellLogger = appShellLogger;

            Application.Current!.UserAppTheme = AppTheme.Dark;
        }

        private static void RegisterCrashHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                WriteCrashLog("UnhandledException", ex?.ToString() ?? args.ExceptionObject?.ToString() ?? "unknown");
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                WriteCrashLog("UnobservedTaskException", args.Exception?.ToString() ?? "unknown");
                args.SetObserved(); // evita que o processo seja encerrado
            };
        }

        private static void WriteCrashLog(string source, string content)
        {
            try
            {
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}]{Environment.NewLine}{content}{Environment.NewLine}{new string('-', 80)}{Environment.NewLine}";
                File.AppendAllText(CrashLogPath, entry);
            }
            catch { /* não pode crashar o crash handler */ }
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

            var appShellVM = new AppShellVM(UserSessionService, _buildDbService, _syncService);
            await appShellVM.UserFlyoutAsync();

            // Se o usuário já está logado, inicia o sync em background
            var user = await UserSessionService.GetCurrentUserAsync();
            if (user != null)
                _syncService.StartThread();

            // Só navega para o Shell após tudo pronto
            window.Page = new AppShell(appShellVM, _appShellLogger);
        }
    }
}