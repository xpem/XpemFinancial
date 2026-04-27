using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;

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

            builder.Services.ShellRoutes();

            return builder.Build();
        }

        public static IServiceCollection ShellRoutes(this IServiceCollection services)
        {
            services.AddTransientWithShellRoute<Views.MainPage, VMs.MainPageVM>(nameof(Views.MainPage));
            services.AddTransientWithShellRoute<Views.TransactionEdit, VMs.TransactionEditVM>(nameof(Views.TransactionEdit));
            return services;
        }
    }
}
