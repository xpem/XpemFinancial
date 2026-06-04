using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.DTO;
using Model.Resp.Api;
using Service;
using System.Net.Http;
using XpemFinancial.Views;

namespace XpemFinancial.VMs
{
    public partial class SignInVM(IUserService userService, IUserSessionService userSessionService) : VMBase
    {
        [ObservableProperty] private string email;
        [ObservableProperty] private string password;
        [ObservableProperty] private string errorMessage;
        [ObservableProperty] private bool errorMessageIsVisible;
        [ObservableProperty] private string signInText = "Acessar";
        [ObservableProperty] private bool btnSignEnabled = true;
        [ObservableProperty] private string version = ((App)Application.Current)!.Version;
        [ObservableProperty] private bool isRequired;

        private async Task<bool> VerrifyFields()
        {
            bool isValid = true;

            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password))
            {
                isValid = false;
            }

            if (!(Connectivity.NetworkAccess == NetworkAccess.Internet))
            {
                ErrorMessageIsVisible = true;
                ErrorMessage = "É necessário ter acesso a internet para efetuar o primeiro acesso.";
                isValid = false;
            }

            if (!isValid)
                IsRequired = true;
            else
                IsRequired = false;

            return isValid;
        }

        [RelayCommand]
        private async Task SignIn()
        {
            IsBusy = true;
            try
            {
                if (!await VerrifyFields())
                    return;

                SignInText = "Acessando...";
                BtnSignEnabled = false;

                var resp = await userService.SignInAsync(Email, Password);

                if (resp.Success)
                {
                    if (resp.Content is not null and UserDTO user)
                        userSessionService.GetCurrentUserAsync().Wait();

                    await Shell.Current.GoToAsync($"{nameof(FirstSyncProcessPage)}", false);
                }
                else
                {
                    string errorMessage = "";

                    if (resp.Content is not null and ErrorTypes error)
                    {
                        if (error == ErrorTypes.WrongEmailOrPassword)
                            errorMessage = "Email ou senha incorretos";
                        else if (error == ErrorTypes.ServerUnavaliable)
                            errorMessage = "Servidor indisponível";
                    }
                    else throw new Exception("Invalid Content");

                    ErrorMessageIsVisible = true;
                    ErrorMessage = errorMessage;
                }
            }
            catch (HttpRequestException)
            {
                ErrorMessageIsVisible = true;
                ErrorMessage = "Não foi possível conectar ao servidor. Verifique sua conexão.";
            }
            catch (Exception)
            {
                ErrorMessageIsVisible = true;
                ErrorMessage = "Ocorreu um erro inesperado. Tente novamente.";
            }
            finally
            {
                BtnSignEnabled = true;
                SignInText = "Acessar";
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task CreateUser() =>  await Shell.Current.GoToAsync($"{nameof(SignUpPage)}");

        [RelayCommand]
        private async Task UpdatePassword() => await Shell.Current.GoToAsync($"{nameof(UpdatePasswordPage)}");

    }
}
