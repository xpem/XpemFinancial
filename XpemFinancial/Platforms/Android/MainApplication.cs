using Android.App;
using Android.Runtime;

namespace XpemFinancial
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
            // Captura exceções não tratadas na thread nativa do Android (JVM).
            // Isso inclui XamlParseException lançadas durante navegação do Shell,
            // que o AppDomain.UnhandledException não consegue capturar de forma confiável.
            AndroidEnvironment.UnhandledExceptionRaiser += OnAndroidUnhandledException;
        }

        private static void OnAndroidUnhandledException(object? sender, RaiseThrowableEventArgs e)
        {
            try
            {
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [AndroidUnhandledException]{Environment.NewLine}{e.Exception}{Environment.NewLine}{new string('-', 80)}{Environment.NewLine}";
                File.AppendAllText(App.CrashLogPath, entry);
            }
            catch { /* não pode crashar o crash handler */ }

            // e.Handled = true evitaria o encerramento do processo.
            // Deixamos false para preservar o comportamento padrão do Android.
            e.Handled = false;
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
