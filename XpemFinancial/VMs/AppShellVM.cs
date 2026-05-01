using CommunityToolkit.Mvvm.ComponentModel;
using Model.DTO;
using Service;
using System;
using System.Collections.Generic;
using System.Text;

namespace XpemFinancial.VMs
{
    public partial class AppShellVM(Service.IUserService UserService) : ObservableObject
    {
        [ObservableProperty]
        private string email;

        [ObservableProperty]
        private string name;

        public async Task UserFlyoutAsync()
        {
            UserDTO? user = await UserService.GetAsync();

            if (user is not null)
            {
                Name = user.Name;
                Email = user.Email;
            }
        }
    }
}
