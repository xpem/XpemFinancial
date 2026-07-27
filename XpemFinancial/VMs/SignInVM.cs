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

        // TODO: Substituir pelo Client ID Android do Google Cloud Console
        private const string GoogleClientId = "314405769421-df6dqs151nbfs2e1rs0hi4o19t5h6frc.apps.googleusercontent.com";
        private const string RedirectUri = "com.xpem.xpemfinancial:/oauth2redirect";

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
            catch (HttpRequestException ex)
            {
                ErrorMessageIsVisible = true;
                ErrorMessage = $"Conexão: {ex.Message}";
            }
            catch (Exception ex)
            {
                ErrorMessageIsVisible = true;
                ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
            }
            finally
            {
                BtnSignEnabled = true;
                SignInText = "Acessar";
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SignInWithGoogle()
        {
            IsBusy = true;
            ErrorMessageIsVisible = false;
            BtnSignEnabled = false;

            try
            {
                if (!(Connectivity.NetworkAccess == NetworkAccess.Internet))
                {
                    ErrorMessageIsVisible = true;
                    ErrorMessage = "É necessário ter acesso a internet.";
                    return;
                }

                var authResult = await WebAuthenticator.Default.AuthenticateAsync(
                    new WebAuthenticatorOptions
                    {
                        Url = new Uri($"https://accounts.google.com/o/oauth2/v2/auth?client_id={GoogleClientId}&redirect_uri={RedirectUri}&response_type=id_token&scope=openid+email+profile&nonce={Guid.NewGuid()}"),
                        CallbackUrl = new Uri(RedirectUri)
                    });

                string? idToken = authResult?.IdToken;

                if (string.IsNullOrWhiteSpace(idToken))
                    idToken = authResult?.Properties.GetValueOrDefault("id_token");

                if (string.IsNullOrWhiteSpace(idToken))
                {
                    ErrorMessageIsVisible = true;
                    ErrorMessage = "Não foi possível obter o token do Google.";
                    return;
                }

                var resp = await userService.SignInWithGoogleAsync(idToken);

                if (resp.Success)
                {
                    if (resp.Content is not null and UserDTO user)
                        userSessionService.GetCurrentUserAsync().Wait();

                    await Shell.Current.GoToAsync($"{nameof(FirstSyncProcessPage)}", false);
                }
                else
                {
                    if (resp.Content is not null and ErrorTypes error)
                    {
                        ErrorMessageIsVisible = true;
                        ErrorMessage = error switch
                        {
                            ErrorTypes.GoogleAuthEmailLinkedToPassword => "Este email já está vinculado a login com senha. Use email e senha.",
                            ErrorTypes.ServerUnavaliable => "Servidor indisponível",
                            _ => "Erro ao autenticar com Google"
                        };
                    }
                    else
                    {
                        ErrorMessageIsVisible = true;
                        ErrorMessage = "Erro ao autenticar com Google";
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // Usuário cancelou o login
            }
            catch (HttpRequestException ex)
            {
                ErrorMessageIsVisible = true;
                ErrorMessage = $"Conexão: {ex.Message}";
            }
            catch (Exception ex)
            {
                ErrorMessageIsVisible = true;
                ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
            }
            finally
            {
                BtnSignEnabled = true;
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task CreateUser() =>  await Shell.Current.GoToAsync($"{nameof(SignUpPage)}");

        [RelayCommand]
        private async Task UpdatePassword() => await Shell.Current.GoToAsync($"{nameof(UpdatePasswordPage)}");

    }
}
