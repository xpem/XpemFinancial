using ApiRepo;
using CommunityToolkit.Mvvm.ComponentModel;
using Service;
using System;
using System.Collections.Generic;
using System.Text;
using XpemFinancial.Views;

namespace XpemFinancial.VMs
{
    public partial class FirstSyncProcessVM(IUserApiRepo userApiRepo,IUserSessionService userSessionService) : VMBase
    {
        [ObservableProperty] private decimal progress;

        public async Task SynchronizingProcess()
        {
            try
            {
                var user = await userSessionService.GetCurrentUserAsync();

                if (user != null)
                {
                    if (Connectivity.NetworkAccess == NetworkAccess.Internet)
                    {
                        //await BooksSyncBLL.ApiToLocalSync(user.Id, user.LastUpdate);

                        Progress = 0.25M;

                        //await BooksSyncBLL.LocalToApiSync();

                        Progress = 0.5M;

                        //await BookHistoricSyncBLL.ApiToLocalSync(user.Id, user.LastUpdate);

                        Progress = 0.75M;

                        //UserBLL.UpdateLocalUserLastUpdate(user.Id);

                        Progress = 1;

                        //_ = Task.Run(() => { Task.Delay(5000); SyncServices.StartThread(); });

                        //_ = AppShellVM.AtualizaUserShowData();

                        _ = Shell.Current.GoToAsync($"//{nameof(MainPage)}");

                    }
                }
            }
            catch (Exception ex) { throw ex; }
        }
    }
}
