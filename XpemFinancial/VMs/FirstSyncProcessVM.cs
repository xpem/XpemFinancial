using ApiRepo;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Service;
using Service.Category;
using System;
using System.Collections.Generic;
using System.Text;
using XpemFinancial.Messages;
using XpemFinancial.Views;

namespace XpemFinancial.VMs
{
    public partial class FirstSyncProcessVM(IUserApiRepo userApiRepo,IUserSessionService userSessionService,ICategorySyncService categorySyncService) : VMBase
    {
        [ObservableProperty] private decimal progress;

        public async Task SyncProcess()
        {
            try
            {
                var user = await userSessionService.GetCurrentUserAsync();

                if (user != null)
                {
                    if (Connectivity.NetworkAccess == NetworkAccess.Internet)
                    {
                        await categorySyncService.PullAsync(user.Id);

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

                        WeakReferenceMessenger.Default.Send(new UserLoggedInMessage());

                    }
                }
            }
            catch (Exception ex) { throw ex; }
        }
    }
}
