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

        // O app abre o endpoint do servidor, que cuida de todo o OAuth com o Google.
        // O servidor redireciona de volta via deep link com o token da API já pronto.
        private const string GoogleSignInStartUrl = "https://xpem.com.br/api/user/session/google/start";
        private const string CallbackUri = "com.xpem.xpemfinancial://oauth2";

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
                    string errorDetail = $"Email: {Email}, ErrorType: {resp.Content}, RawContent: {resp.Content?.ToString() ?? "null"}";
                    App.WriteCrashLog("SignIn.Failed", errorDetail);

                    ErrorMessageIsVisible = true;
                    ErrorMessage = resp.Content switch
                    {
                        ErrorTypes.WrongEmailOrPassword => "Email ou senha incorretos",
                        string s when s.StartsWith(ErrorTypes.ServerUnavaliable.ToString()) => $"Servidor indisponível: {s}",
                        _ => $"Erro desconhecido: {resp.Content}"
                    };
                }
            }
            catch (HttpRequestException ex)
            {
                App.WriteCrashLog("SignIn.HttpRequestException", $"Email: {Email}{Environment.NewLine}{ex}");
                ErrorMessageIsVisible = true;
                ErrorMessage = $"Conexão: {ex.Message}";
            }
            catch (Exception ex)
            {
                App.WriteCrashLog("SignIn.Exception", $"Email: {Email}{Environment.NewLine}{ex}");
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

                System.Diagnostics.Debug.WriteLine($"[GoogleSignIn] Abrindo: {GoogleSignInStartUrl}");

                // O servidor cuida de todo o OAuth com o Google e devolve o token da API via deep link
                var authResult = await WebAuthenticator.Default.AuthenticateAsync(
                    new WebAuthenticatorOptions
                    {
                        Url = new Uri(GoogleSignInStartUrl),
                        CallbackUrl = new Uri(CallbackUri)
                    });

                System.Diagnostics.Debug.WriteLine($"[GoogleSignIn] Callback recebido: {string.Join(", ", authResult?.Properties.Select(p => $"{p.Key}={p.Value}") ?? [])}");

                // O servidor devolve erro no query string em caso de falha
                if (authResult?.Properties.TryGetValue("error", out string? googleError) == true && !string.IsNullOrWhiteSpace(googleError))
                {
                    ErrorMessageIsVisible = true;
                    ErrorMessage = "Erro ao autenticar com Google.";
                    System.Diagnostics.Debug.WriteLine($"[GoogleSignIn] Erro recebido do servidor: {googleError}");
                    return;
                }

                // Servidor devolve token + refreshToken já prontos no deep link — sem segunda chamada à API
                string? apiToken = authResult?.Properties.GetValueOrDefault("token");
                string? refreshToken = authResult?.Properties.GetValueOrDefault("refreshToken");

                System.Diagnostics.Debug.WriteLine($"[GoogleSignIn] token: {(string.IsNullOrWhiteSpace(apiToken) ? "NULO" : "OK")}");

                if (string.IsNullOrWhiteSpace(apiToken))
                {
                    ErrorMessageIsVisible = true;
                    ErrorMessage = "Não foi possível obter o token da sessão.";
                    return;
                }

                var resp = await userService.SignInWithGoogleTokenAsync(apiToken, refreshToken);

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
                            ErrorTypes.ServerUnavaliable => $"Servidor indisponível: {resp.Content}",
                            _ => $"Erro ao autenticar com Google: {resp.Content}"
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
                System.Diagnostics.Debug.WriteLine("[GoogleSignIn] Cancelado pelo usuário.");
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GoogleSignIn] HttpRequestException: {ex.Message}");
                ErrorMessageIsVisible = true;
                ErrorMessage = $"Conexão: {ex.Message}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GoogleSignIn] Exception: {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[GoogleSignIn] StackTrace: {ex.StackTrace}");
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
