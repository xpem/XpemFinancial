using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Service;
using XpemFinancial.Utils;
using XpemFinancial.Utils.Services;
using XpemFinancial.Views;
using XpemFinancial.Views.Account;
using XpemFinancial.VMs;

namespace XpemFinancial
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // fonts: https://fonts.google.com/specimen/Playfair+Display
            // icons: https://fontawesome.com/icons/right-to-bracket?s=solid
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
#if !WINDOWS
                .UseMauiCommunityToolkit(options =>
                {
                    options.SetShouldEnableSnackbarOnWindows(true);
                })
#else
                .UseMauiCommunityToolkit()
#endif
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("Free-Solid-900.otf", "Icons");
                });

#if ANDROID
            builder.ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler<DatePicker, XpemFinancial.Platforms.Android.ThemedDatePickerHandler>();
            });
#endif

            builder.Logging.AddProvider(new CrashFileLoggerProvider());
            builder.Logging.AddFilter("Microsoft.WindowsAppRuntime", LogLevel.None);

#if DEBUG
            builder.Logging.AddDebug();
#endif

            builder.Services.AddDbContext();
            builder.Services.AddService();
            builder.Services.AddRepo();
            builder.Services.AddApiRepo();
            builder.Services.AddSingleton<SyncService>();
            builder.Services.AddShellRoutes();

            return builder.Build();
        }

        public static IServiceCollection AddShellRoutes(this IServiceCollection services)
        {
            services.AddTransientWithShellRoute<MainPage, MainVM>(nameof(MainPage));
            services.AddTransientWithShellRoute<Views.TransactionEditPage, VMs.TransactionEditVM>(nameof(Views.TransactionEditPage));
            services.AddTransientWithShellRoute<Views.CategoryPicker, VMs.CategoryPickerVM>(nameof(Views.CategoryPicker));
            services.AddTransientWithShellRoute<Views.CategoryEditPage, VMs.CategoryEditVM>(nameof(Views.CategoryEditPage));
            services.AddTransientWithShellRoute<Views.CategoryManagementPage, VMs.CategoryManagementVM>(nameof(Views.CategoryManagementPage));
            services.AddTransientWithShellRoute<AccountsPage, VMs.AccountsVM>(nameof(AccountsPage));
            services.AddTransientWithShellRoute<AccountEditPage, VMs.AccountEditVM>(nameof(AccountEditPage));
            services.AddTransientWithShellRoute<Views.SignInPage, VMs.SignInVM>(nameof(Views.SignInPage));
            services.AddTransientWithShellRoute<Views.FirstSyncProcessPage, VMs.FirstSyncProcessVM>(nameof(Views.FirstSyncProcessPage));
            services.AddTransientWithShellRoute<Views.SignUpPage, VMs.SignUpVM>(nameof(Views.SignUpPage));
            services.AddTransientWithShellRoute<Views.UpdatePasswordPage, VMs.UpdatePasswordVM>(nameof(Views.UpdatePasswordPage));
            services.AddTransientWithShellRoute<Views.ChartPage, VMs.ChartVM>(nameof(Views.ChartPage));

            return services;
        }
    }
}
