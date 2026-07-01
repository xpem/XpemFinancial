using Model.DTO;
using Service;
using Service.Account;
using Service.Category;
using Service.Recurring;
using Service.Transaction;
using System.Diagnostics;

namespace XpemFinancial.Utils.Services
{
    /// <summary>
    /// Singleton service responsible for periodically syncing the local database with the server.
    /// Registered as Singleton in DI — do not inject as Transient.
    /// </summary>
    public class SyncService(
        IUserSessionService userSessionService,
        IUserService userService,
        ICategoryService categoryService,
        IAccountService accountService,
        IRecurringRuleService recurringRuleService,
        ITransactionService transactionService)
    {
        // ── sync status ───────────────────────────────────────────────────────
        // Fix #3: use a volatile int so reads/writes from different threads are
        // always visible and the compiler cannot reorder around them.
        // Mapping: 0 = Sleeping, 1 = Processing, 2 = ServerOff, 3 = Unauthorized
        private volatile int _syncStatus = (int)SyncStatus.Sleeping;

        public SyncStatus Synchronizing
        {
            get => (SyncStatus)_syncStatus;
            private set => Interlocked.Exchange(ref _syncStatus, (int)value);
        }

        // ── timer ─────────────────────────────────────────────────────────────
        public Timer? Timer { get; private set; }

        // 30 seconds
        private readonly int _interval = 30_000;

        // 0 = not running, 1 = running
        private int _threadStarted = 0;

        public bool ThreadIsRunning => _threadStarted == 1;

        /// <summary>
        /// Raised on the thread-pool after each completed sync cycle (success or handled error).
        /// Subscribers that need to update the UI should marshal back to the main thread themselves.
        /// </summary>
        public event Action? SyncCompleted;

        // ── lifecycle ─────────────────────────────────────────────────────────

        public void StartThread()
        {
            if (Interlocked.CompareExchange(ref _threadStarted, 1, 0) == 0)
            {
                Synchronizing = SyncStatus.Sleeping;
                Thread thread = new(SetTimer) { IsBackground = true };
                thread.Start();
            }
        }

        private void SetTimer()
        {
            // Run an immediate cycle, then schedule repeating ones.
            // Fix #1: fire-and-forget is safe here because TimerCallback is the async boundary —
            // we use a plain Task-returning method and attach a continuation that logs/swallows
            // instead of letting the exception escape an async void.
            FireAndForget(ExecSyncAsync());
            Timer = new Timer(_ => FireAndForget(ExecSyncAsync()), null, _interval, Timeout.Infinite);
        }

        // Fix #1: replaces async void SyncLocalDb — all exceptions are caught here so they
        // can never propagate unobserved and crash the process.
        private static void FireAndForget(Task task)
        {
            task.ContinueWith(
                t => Debug.WriteLine($"[SyncService] Unhandled exception: {t.Exception}"),
                TaskContinuationOptions.OnlyOnFaulted);
        }

        // ── core sync ─────────────────────────────────────────────────────────

        public async Task ExecSyncAsync()
        {
            try
            {
                UserDTO? user = await userSessionService.GetCurrentUserAsync().ConfigureAwait(false);

                // Fix #3: compare-exchange so only one concurrent invocation enters Processing
                if (user == null || Interlocked.CompareExchange(ref _syncStatus,
                        (int)SyncStatus.Processing,
                        (int)SyncStatus.Sleeping) != (int)SyncStatus.Sleeping)
                    return;

                try
                {
                    if (Connectivity.NetworkAccess == NetworkAccess.Internet)
                    {
                        // Crash recovery: registros travados em "Pushing" voltam para "Pending"
                        await transactionService.ResetStuckPushingAsync().ConfigureAwait(false);

                        await userService.UpdateLastUpdate(user.Id).ConfigureAwait(false);

                        await categoryService.PushAsync().ConfigureAwait(false);
                        DateTime categoryLastUpdate = await categoryService.GetLastUpdatedAtAsync().ConfigureAwait(false);
                        await categoryService.PullAsync(user.Id, categoryLastUpdate).ConfigureAwait(false);

                        try
                        {
                            await accountService.PushPendingAsync(user.Id).ConfigureAwait(false);
                            await accountService.PullAsync(user.Id).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            // Account sync falhou → interromper RecurringRules e Transactions (Req 9.4)
                            Debug.WriteLine($"[SyncService] Account sync failed, skipping dependents: {ex.Message}");
                            return;
                        }

                        DateTime recurringLastUpdate = await recurringRuleService.GetLastUpdatedAtAsync().ConfigureAwait(false);
                        await recurringRuleService.PullAsync(user.Id, recurringLastUpdate).ConfigureAwait(false);

                        await transactionService.PushPendingAsync(user.Id).ConfigureAwait(false);

                        DateTime transactionLastUpdate = await transactionService.GetLastUpdatedAtAsync().ConfigureAwait(false);
                        await transactionService.PullAsync(user.Id, transactionLastUpdate).ConfigureAwait(false);
                    }
                }
                finally
                {
                    // Always leave Processing — only overwrite if we haven't been set to
                    // Unauthorized by the catch block below.
                    Interlocked.CompareExchange(ref _syncStatus,
                        (int)SyncStatus.Sleeping,
                        (int)SyncStatus.Processing);
                }
            }
            catch (HttpRequestException ex)
            {
                bool refused = ex.InnerException?.Message.Contains(
                    "No connection could be made because the target machine actively refused it.",
                    StringComparison.OrdinalIgnoreCase) == true;

                Synchronizing = refused ? SyncStatus.ServerOff : SyncStatus.Sleeping;

                Debug.WriteLine($"[SyncService] HTTP error during sync: {ex.Message}");
            }
            catch (UnauthorizedAccessException)
            {
                Synchronizing = SyncStatus.Unauthorized;
            }
            catch (Exception ex)
            {
                // Fix #1: swallow unexpected exceptions at the async boundary — do NOT rethrow
                // from a timer callback path, as that would crash the process via an unobserved
                // task exception. Log and continue; the next timer tick will retry.
                Synchronizing = SyncStatus.Sleeping;
                Debug.WriteLine($"[SyncService] Unexpected sync error: {ex}");
            }
            finally
            {
                // Reschedule the next tick unconditionally.
                Timer?.Change(_interval, Timeout.Infinite);

                // Fix #4: notify subscribers that the sync cycle ended so they can reload data.
                SyncCompleted?.Invoke();
            }
        }

        public void Stop()
        {
            Timer?.Dispose();
            Timer = null;
            Interlocked.Exchange(ref _threadStarted, 0);
            Synchronizing = SyncStatus.Sleeping;
        }
    }

    public enum SyncStatus
    {
        Processing, Sleeping, ServerOff, Unauthorized
    }
}
