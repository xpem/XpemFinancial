using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.DTO;
using Service;
using XpemFinancial.Views;

namespace XpemFinancial.VMs
{
    public partial class AppShellVM(Service.IUserSessionService userSessionService, IBuildDbService buildDbService) : ObservableObject
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

        [RelayCommand]
        private async Task SignOut()
        {
            bool resp = await Application.Current.Windows[0].Page.DisplayAlertAsync("Confirmação", "Deseja sair e retornar a tela inicial?", "Sim", "Cancelar");

            if (resp)
            {
                //finalize sync thread process
                //syncService.ThreadIsRunning = false;

                //syncService.Timer?.Dispose();

                userSessionService.Invalidate();

                await buildDbService.CleanLocalDatabaseAsync();

                _ = Shell.Current.GoToAsync($"//{nameof(SignInPage)}");
            }
        }
    }
}
