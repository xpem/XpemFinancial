using Microsoft.Extensions.Logging;

namespace XpemFinancial.Utils
{
    /// <summary>
    /// Grava no crash.log qualquer mensagem de nível Error ou Critical emitida
    /// pelo runtime do MAUI — inclui XamlParseException, erros de binding,
    /// falhas de navegação e qualquer exceção não tratada passada ao ILogger.
    /// </summary>
    public sealed class CrashFileLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CrashFileLogger(categoryName);
        public void Dispose() { }
    }

    internal sealed class CrashFileLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            try
            {
                string message = formatter(state, exception);
                string exDetail = exception is not null
                    ? $"{Environment.NewLine}{exception}"
                    : string.Empty;

                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{logLevel}] [{category}]{Environment.NewLine}{message}{exDetail}{Environment.NewLine}{new string('-', 80)}{Environment.NewLine}";

                File.AppendAllText(App.CrashLogPath, entry);
            }
            catch { /* não pode crashar o logger */ }
        }
    }
}
