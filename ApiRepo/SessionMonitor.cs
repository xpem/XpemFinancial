namespace ApiRepo
{
    /// <summary>
    /// Thrown when the session can no longer be renewed (the refresh token is expired/invalid).
    /// It is an <see cref="UnauthorizedAccessException"/> so the existing handlers keep working.
    /// </summary>
    public class SessionExpiredException(string? message = null)
        : UnauthorizedAccessException(message ?? "Sessão expirada. Faça login novamente.");

    /// <summary>
    /// Single source of truth for "the session is expired". Set by the authenticated HTTP pipeline the
    /// moment the refresh token is rejected, and cleared when the user signs in again.
    /// While expired, authenticated requests fail fast without touching the network, so the background
    /// sync does not keep hammering the server (401 + refresh attempt on every request, every 30s).
    /// </summary>
    public static class SessionMonitor
    {
        private static int _expired;

        public static bool IsExpired => Volatile.Read(ref _expired) == 1;

        /// <summary>Raised once per expiration (on the thread that detected it).</summary>
        public static event Action? SessionExpired;

        public static void MarkExpired()
        {
            if (Interlocked.Exchange(ref _expired, 1) == 1)
                return;

            try { SessionExpired?.Invoke(); }
            catch { /* a faulty subscriber must never break the request pipeline */ }
        }

        /// <summary>Call after a successful sign-in.</summary>
        public static void Reset() => Interlocked.Exchange(ref _expired, 0);

        public static void ThrowIfExpired()
        {
            if (IsExpired)
                throw new SessionExpiredException();
        }
    }
}
