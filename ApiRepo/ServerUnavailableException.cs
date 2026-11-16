namespace ApiRepo
{
    /// <summary>
    /// Thrown when a write could not be delivered because the server was unreachable
    /// (timeout, DNS, connection refused...). Callers must keep the local record as
    /// pending so the background sync retries it, instead of treating it as synced.
    /// </summary>
    public class ServerUnavailableException(string message) : Exception(message);
}
