using CommunityToolkit.Mvvm.ComponentModel;
using Model.DTO;
using Service;
using System;
using System.Collections.Generic;
using System.Text;

namespace XpemFinancial.VMs
{
    public partial class AppShellVM(Service.IUserSessionService userSessionService) : ObservableObject
    {
        [ObservableProperty]
        private string email;

        [ObservableProperty]
        private string name;

        public async Task UserFlyoutAsync()
        {
            UserDTO? user = await userSessionService.GetCurrentUserAsync();

            if (user is not null)
            {
                Name = user.Name;
                Email = user.Email;
            }
        }
    }
}
