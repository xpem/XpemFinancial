using XpemFinancial.Views;
using XpemFinancial.VMs;

namespace XpemFinancial.Utils
{
    /// <summary>
    /// Single place that reacts to an expired session in the UI. Replaces the same
    /// "show message + navigate to sign-in" block that used to be copied into every ViewModel.
    /// Local data is kept: signing in again with the same account resumes the sync and pushes
    /// whatever is still pending.
    /// </summary>
    public static class SessionExpiredHandler
    {
        private const string Message = "Sessão expirada. Faça login novamente.";

        // Several callers (a ViewModel and the background sync) can notice the expiration at the same time.
        private static int _handling;

        /// <summary>User-initiated action failed because the session is gone: tell the user and go to sign-in.</summary>
        public static async Task HandleAsync()
        {
            if (Interlocked.CompareExchange(ref _handling, 1, 0) != 0)
                return;

            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await VMBase.ShowMessage("Sessão expirada", Message);

                    if (!IsOnAuthPage())
                        await Shell.Current.GoToAsync($"//{nameof(SignInPage)}");
                });
            }
            finally
            {
                Interlocked.Exchange(ref _handling, 0);
            }
        }

        /// <summary>
        /// The background sync noticed the expiration. Do not yank the user out of what they are doing
        /// (e.g. a half-filled form): just say so; the sync icon already shows the error state.
        /// </summary>
        public static async Task NotifyBackgroundAsync()
        {
            if (Interlocked.CompareExchange(ref _handling, 1, 0) != 0)
                return;

            try
            {
                // The sync can detect the expiration while the app is still starting. Wait (briefly) for
                // the window to be ready; if it never is, the sync icon still shows the error state.
                for (int i = 0; i < 20 && !await MainThread.InvokeOnMainThreadAsync(VMBase.CanShowAlert); i++)
                    await Task.Delay(500);

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (!IsOnAuthPage() && VMBase.CanShowAlert())
                        await VMBase.ShowMessage("Sessão expirada", "Sessão expirada. Entre novamente para continuar sincronizando.");
                });
            }
            finally
            {
                Interlocked.Exchange(ref _handling, 0);
            }
        }

        private static bool IsOnAuthPage()
        {
            string? location = Shell.Current?.CurrentState?.Location?.OriginalString;

            return location is not null
                && (location.Contains(nameof(SignInPage), StringComparison.OrdinalIgnoreCase)
                    || location.Contains(nameof(FirstSyncProcessPage), StringComparison.OrdinalIgnoreCase));
        }
    }
}
