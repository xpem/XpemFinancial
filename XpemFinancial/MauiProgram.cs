using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using Service.DI;

namespace XpemFinancial
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            //fonts: https://fonts.google.com/specimen/Playfair+Display
            //icons: https://fontawesome.com/icons/right-to-bracket?s=solid
            var builder = MauiApp.CreateBuilder();

            builder.UseMauiApp<App>().UseMauiCommunityToolkit();

            builder
                .UseMauiApp<App>()
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
            builder.Services.ShellRoutes();

            return builder.Build();
        }

        public static IServiceCollection ShellRoutes(this IServiceCollection services)
        {
            // MainPage é raiz do Shell (AppShell.xaml) — registrar só no DI, não como rota
            services.AddTransient<Views.MainPage>();
            services.AddTransient<VMs.MainVM>();

            // Páginas de navegação secundária
            services.AddTransientWithShellRoute<Views.TransactionEdit, VMs.TransactionEditVM>(nameof(Views.TransactionEdit));
            services.AddTransientWithShellRoute<Views.CategoryPicker, VMs.CategoryPickerVM>(nameof(Views.CategoryPicker));
            services.AddTransientWithShellRoute<Views.AccountPage, VMs.AccountVM>(nameof(Views.AccountPage));
            return services;
        }
    }
}
