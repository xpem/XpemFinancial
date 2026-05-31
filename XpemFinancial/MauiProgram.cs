using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Service;
using XpemFinancial.Utils;
using XpemFinancial.Views;
using XpemFinancial.VMs;

namespace XpemFinancial
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            //fonts: https://fonts.google.com/specimen/Playfair+Display
            //icons: https://fontawesome.com/icons/right-to-bracket?s=solid
            var builder = MauiApp.CreateBuilder();

            builder.UseMauiApp<App>().UseMauiCommunityToolkit(options =>
            {
                options.SetShouldEnableSnackbarOnWindows(true);
            });

            builder
                .UseMauiApp<App>()
                .ConfigureMauiHandlers(handlers =>
                {
#if ANDROID
                    handlers.AddHandler<DatePicker, XpemFinancial.Platforms.Android.ThemedDatePickerHandler>();
#endif
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("Free-Solid-900.otf", "Icons");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            builder.Services.AddDbContext();
            builder.Services.AddService();
            builder.Services.AddRepo();
            builder.Services.AddApiRepo();
            builder.Services.AddSingleton<SyncService>();
            builder.Services.ShellRoutes();

            return builder.Build();
        }

        public static IServiceCollection ShellRoutes(this IServiceCollection services)
        {
            // MainPage é raiz do Shell (AppShell.xaml) — registrar só no DI, não como rota
            //services.AddTransient<Views.MainPage>();
            //services.AddTransient<VMs.MainVM>();

            // Páginas de navegação secundária
            services.AddTransientWithShellRoute<MainPage, MainVM>(nameof(MainPage));
            services.AddTransientWithShellRoute<Views.TransactionEditPage, VMs.TransactionEditVM>(nameof(Views.TransactionEditPage));
            services.AddTransientWithShellRoute<Views.CategoryPicker, VMs.CategoryPickerVM>(nameof(Views.CategoryPicker));
            services.AddTransientWithShellRoute<Views.AccountPage, VMs.AccountVM>(nameof(Views.AccountPage));
            services.AddTransientWithShellRoute<Views.SignInPage, VMs.SignInVM>(nameof(Views.SignInPage));
            services.AddTransientWithShellRoute<Views.FirstSyncProcessPage, VMs.FirstSyncProcessVM>(nameof(Views.FirstSyncProcessPage));
            services.AddTransientWithShellRoute<Views.SignUpPage, VMs.SignUpVM>(nameof(Views.SignUpPage));
            return services;
        }
    }
}
