using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Model.DTO;
using Service;
using XpemFinancial.Messages;
using XpemFinancial.Utils.Services;
using XpemFinancial.Views;

namespace XpemFinancial.VMs
{
    public partial class AppShellVM(IUserSessionService userSessionService, IBuildDbService buildDbService, SyncService syncService) : ObservableObject
    {
        [ObservableProperty]
        private string? email;

        [ObservableProperty]
        private string? name;

        public async Task UserFlyoutAsync()
        {
            try
            {
                UserDTO? user = await userSessionService.GetCurrentUserAsync();

                if (user is not null)
                {
                    Name = user.Name;
                    Email = user.Email;
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., log the error, show a message to the user, etc.)
                Console.WriteLine($"Error fetching user data: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task SignOut()
        {
            bool resp = await Application.Current.Windows[0].Page.DisplayAlertAsync("Confirmação", "Deseja sair e retornar a tela inicial?", "Sim", "Cancelar");

            if (resp)
            {
                syncService.Stop();

                userSessionService.Invalidate();

                await buildDbService.CleanLocalDatabaseAsync();

                _ = Shell.Current.GoToAsync($"//{nameof(SignInPage)}");
            }
        }

        public void Init()
        {            
            WeakReferenceMessenger.Default.Register<UserLoggedInMessage>(this, async (r, m) =>
            {
                await UserFlyoutAsync();
            });
        }
    }
}
