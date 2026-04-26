using Microsoft.Extensions.Logging;

namespace XpemFinancial
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            //fonts: https://fonts.google.com/specimen/Playfair+Display
            //icons: https://fontawesome.com/icons/right-to-bracket?s=solid
            var builder = MauiApp.CreateBuilder();
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

            return builder.Build();
        }
    }
}
