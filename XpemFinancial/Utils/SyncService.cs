using Model.DTO;
using Service;
using Service.Account;
using Service.Category;
using Service.Recurring;
using Service.Transaction;

namespace XpemFinancial.Utils
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
        // Global sync status — safe to be instance-level since the class is Singleton
        public SyncStatus Synchronizing { get; private set; } = SyncStatus.Sleeping;

        public Timer? Timer { get; private set; }

        // 30 seconds
        readonly int Interval = 30000;

        // 0 = not running, 1 = running — used with Interlocked to avoid race condition on startup
        private int _threadStarted = 0;

        public bool ThreadIsRunning => _threadStarted == 1;

        public void StartThread()
        {
            // Interlocked.CompareExchange ensures only one thread can transition 0 → 1
            if (Interlocked.CompareExchange(ref _threadStarted, 1, 0) == 0)
            {
                Synchronizing = SyncStatus.Sleeping;

                Thread thread = new(SetTimer) { IsBackground = true };
                thread.Start();
            }
        }

        private void SetTimer()
        {
            SyncLocalDb(null);
            Timer = new Timer(SyncLocalDb, null, Interval, Timeout.Infinite);
        }

        // async void is required by TimerCallback signature.
        // Exceptions are handled inside ExecSyncAsync — unhandled ones would crash the process.
        public async void SyncLocalDb(object? state) => await ExecSyncAsync();

        public async Task ExecSyncAsync()
        {
            try
            {
                UserDTO? user = await userSessionService.GetCurrentUserAsync();

                if (user != null && Synchronizing != SyncStatus.Processing)
                {
                    Synchronizing = SyncStatus.Processing;

                    if (Connectivity.NetworkAccess == NetworkAccess.Internet)
                    {
                        await userService.UpdateLastUpdate(user.Id);

                        DateTime categoryLastUpdate = await categoryService.GetLastUpdatedAtAsync();
                        await categoryService.PullAsync(user.Id, categoryLastUpdate);

                        DateTime accountLastUpdate = await accountService.GetLastUpdatedAtAsync();
                        await accountService.PullAsync(user.Id, accountLastUpdate);

                        DateTime recurringLastUpdate = await recurringRuleService.GetLastUpdatedAtAsync();
                        await recurringRuleService.PullAsync(user.Id, recurringLastUpdate);

                        DateTime transactionLastUpdate = await transactionService.GetLastUpdatedAtAsync();
                        await transactionService.PullAsync(user.Id, transactionLastUpdate);
                    }

                    Synchronizing = SyncStatus.Sleeping;
                }
            }
            catch (HttpRequestException ex)
            {
                if (ex.InnerException != null && ex.InnerException.Message.Contains("No connection could be made because the target machine actively refused it."))
                    Synchronizing = SyncStatus.ServerOff;
                else
                    throw;
            }
            catch (UnauthorizedAccessException)
            {
                Synchronizing = SyncStatus.Unauthorized;
            }
            catch
            {
                throw;
            }
            finally
            {
                // Timer is always rescheduled — even after unexpected exceptions — to keep sync alive.
                // If this is undesirable after a fatal error, stop the timer before rethrowing above.
                Timer?.Change(Interval, Timeout.Infinite);

                if (Synchronizing != SyncStatus.Unauthorized)
                    Synchronizing = SyncStatus.Sleeping;
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
