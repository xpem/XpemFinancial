using ApiRepo;
using Microsoft.Extensions.Logging;
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
        ITransactionService transactionService,
        ILogger<SyncService> logger)
    {
        // ── sync status ───────────────────────────────────────────────────────
        // Fix #3: use a volatile int so reads/writes from different threads are
        // always visible and the compiler cannot reorder around them.
        // Mapping: 0 = Sleeping, 1 = Processing, 2 = ServerOff, 3 = Unauthorized
        // This is only a STATUS for the UI. It must never gate whether a cycle may run: ServerOff and
        // Unauthorized used to be terminal (the cycle only started from Sleeping), which stopped the
        // sync until the app was restarted — even after the user signed in again.
        private volatile int _syncStatus = (int)SyncStatus.Sleeping;

        // Mutual exclusion between cycles: 0 = idle, 1 = a cycle is running.
        private int _cycleRunning = 0;

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
        /// UTC timestamp of the last successfully completed sync cycle.
        /// Null until at least one full cycle has succeeded.
        /// </summary>
        public DateTime? LastSuccessfulSync { get; private set; }

        /// <summary>
        /// Raised on the thread-pool after each completed sync cycle (success or handled error).
        /// Subscribers that need to update the UI should marshal back to the main thread themselves.
        /// </summary>
        public event Action? SyncCompleted;

        /// <summary>
        /// Raised once when the sync detects that the session is no longer valid (refresh token expired).
        /// The sync stays paused until the user signs in again (<see cref="SessionMonitor.Reset"/>).
        /// </summary>
        public event Action? SessionExpiredDetected;

        // ── lifecycle ─────────────────────────────────────────────────────────

        public void StartThread()
        {
            if (Interlocked.CompareExchange(ref _threadStarted, 1, 0) == 0)
            {
                Synchronizing = SyncStatus.Sleeping;
                Thread thread = new(SetTimer) { IsBackground = true };
                thread.Start();
            }
            else
            {
                // The thread is already running (e.g. the user signed in again after the session
                // expired): clear the error status so the next tick starts a normal cycle.
                if (!SessionMonitor.IsExpired)
                {
                    Interlocked.CompareExchange(ref _syncStatus, (int)SyncStatus.Sleeping, (int)SyncStatus.Unauthorized);
                    Interlocked.CompareExchange(ref _syncStatus, (int)SyncStatus.Sleeping, (int)SyncStatus.ServerOff);
                }
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
            bool acquired = false;

            try
            {
                UserDTO? user = await userSessionService.GetCurrentUserAsync().ConfigureAwait(false);

                if (user == null)
                    return;

                // Circuit breaker: with an expired session every request would just fail (401 + a
                // refresh attempt). Stay paused, without touching the network, until the user signs in.
                if (SessionMonitor.IsExpired)
                {
                    MarkUnauthorized();
                    return;
                }

                // Only one cycle at a time (the timer tick and the first-sync can overlap).
                if (Interlocked.CompareExchange(ref _cycleRunning, 1, 0) != 0)
                    return;

                acquired = true;
                Synchronizing = SyncStatus.Processing;

                try
                {
                    if (Connectivity.NetworkAccess == NetworkAccess.Internet)
                    {
                        // Crash recovery: registros travados em "Pushing" voltam para "Pending"
                        await transactionService.ResetStuckPushingAsync().ConfigureAwait(false);

                        await userService.UpdateLastUpdate(user.Id).ConfigureAwait(false);

                        await categoryService.PushPendingAsync(user.Id).ConfigureAwait(false);
                        DateTime categoryLastUpdate = await categoryService.GetLastUpdatedAtAsync().ConfigureAwait(false);
                        await categoryService.PullAsync(user.Id, categoryLastUpdate).ConfigureAwait(false);

                        bool accountSyncOk = true;
                        try
                        {
                            await accountService.PushPendingAsync(user.Id).ConfigureAwait(false);
                            await accountService.PullAsync(user.Id).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            // Account sync falhou → interromper apenas os PULLs dependentes (Req 9.4).
                            // O push de transações pendentes continua abaixo: TransactionService.PushAsync
                            // já adia graciosamente quando falta o ExternalId da conta, então uma falha
                            // temporária na sincronização de contas não deve travar transações já
                            // pendentes indefinidamente (cada ciclo de 30s voltaria a cair aqui).
                            accountSyncOk = false;
                            logger.LogError(ex, "[SyncService] Account sync failed, skipping pull dependents");
                        }

                        // Pushes below swallow individual failures, so make sure an expired session is not
                        // mistaken for "everything fine": abort the cycle and pause the sync.
                        SessionMonitor.ThrowIfExpired();

                        // Regras recorrentes pendentes (criadas/editadas offline) também precisam subir.
                        // Falhas individuais são tratadas dentro do serviço (ficam pendentes para o próximo ciclo).
                        if (accountSyncOk)
                            await recurringRuleService.PushPendingAsync(user.Id).ConfigureAwait(false);

                        // Push de transações pendentes não depende do pull de contas ter sucedido.
                        await transactionService.PushPendingAsync(user.Id).ConfigureAwait(false);

                        if (!accountSyncOk)
                            return;

                        // IMPORTANTE: transações ANTES das regras. O pull de regras gera as ocorrências locais
                        // (com UpdatedAt = agora); se isso acontecesse antes, essas projeções esconderiam as
                        // edições/exclusões de ocorrências feitas em outro dispositivo.
                        DateTime transactionLastUpdate = await transactionService.GetLastUpdatedAtAsync().ConfigureAwait(false);
                        await transactionService.PullAsync(user.Id, transactionLastUpdate).ConfigureAwait(false);

                        DateTime recurringLastUpdate = await recurringRuleService.GetLastUpdatedAtAsync().ConfigureAwait(false);
                        await recurringRuleService.PullAsync(user.Id, recurringLastUpdate).ConfigureAwait(false);

                        // All steps completed — record the successful sync time.
                        LastSuccessfulSync = DateTime.Now;
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
                // Any HTTP-level failure means the server is unreachable — show the warning icon.
                // Previously only "actively refused" was matched, which missed timeouts, DNS
                // failures, SSL errors, and any other transport-layer problems.
                Synchronizing = SyncStatus.ServerOff;

                Debug.WriteLine($"[SyncService] HTTP error during sync: {ex.Message}");
            }
            catch (UnauthorizedAccessException)
            {
                // SessionExpiredException was already registered in SessionMonitor where it was detected,
                // which is what keeps the next ticks paused. Any other UnauthorizedAccessException (e.g. no
                // token stored) only flags the status and is retried on the next tick without a network call.
                MarkUnauthorized();
            }
            catch (Exception ex)
            {
                // Fix #1: swallow unexpected exceptions at the async boundary — do NOT rethrow
                // from a timer callback path, as that would crash the process via an unobserved
                // task exception. Log and continue; the next timer tick will retry.
                Synchronizing = SyncStatus.Sleeping;
                logger.LogError(ex, "[SyncService] Unexpected sync error");
            }
            finally
            {
                if (acquired)
                    Interlocked.Exchange(ref _cycleRunning, 0);

                // Reschedule the next tick unconditionally.
                Timer?.Change(_interval, Timeout.Infinite);

                // Fix #4: notify subscribers that the sync cycle ended so they can reload data.
                SyncCompleted?.Invoke();
            }
        }

        private void MarkUnauthorized()
        {
            bool alreadyKnown = Synchronizing == SyncStatus.Unauthorized;
            Synchronizing = SyncStatus.Unauthorized;

            if (!alreadyKnown)
                SessionExpiredDetected?.Invoke();
        }

        public void Stop()
        {
            Timer?.Dispose();
            Timer = null;
            Interlocked.Exchange(ref _threadStarted, 0);
            Synchronizing = SyncStatus.Sleeping;
            LastSuccessfulSync = null;
        }
    }

    public enum SyncStatus
    {
        Processing, Sleeping, ServerOff, Unauthorized
    }
}
