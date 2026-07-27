using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.Resp.Api;
using Service;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace XpemFinancial.VMs
{
    public partial class SignUpVM(IUserService userService) : VMBase
    {
        [ObservableProperty] private string name;
        [ObservableProperty] private string email;
        [ObservableProperty] private string password;
        [ObservableProperty] private string confirmPassword;
        [ObservableProperty] private bool btnSignUpEnabled = true;
        [ObservableProperty] private bool errorMessageIsVisible;
        [ObservableProperty] private bool isRequired;
        [ObservableProperty] private string errorMessage;

        private bool VerifyFields()
        {
            ErrorMessageIsVisible = false;
            ErrorMessage = string.Empty;

            if (string.IsNullOrEmpty(Name) || string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password) || string.IsNullOrEmpty(ConfirmPassword))
            {
                IsRequired = true;
                ErrorMessageIsVisible = true;
                ErrorMessage = "Preencha todos os campos obrigatórios";
                return false;
            }

            IsRequired = false;

            if (!ValidateEmail(Email))
            {
                ErrorMessageIsVisible = true;
                ErrorMessage = "Digite um email válido";
                return false;
            }

            if (Password is null || Password.Length < 4)
            {
                ErrorMessageIsVisible = true;
                ErrorMessage = "A senha deve ter pelo menos 4 caracteres";
                return false;
            }

            if (ConfirmPassword is null || !ConfirmPassword.Equals(Password, StringComparison.Ordinal))
            {
                ErrorMessageIsVisible = true;
                ErrorMessage = "As senhas não coincidem";
                return false;
            }

            return true;
        }

        public static bool ValidateEmail(string email)
        {
            if (email is null) return false;

                return Regex.IsMatch(email, @"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z", RegexOptions.IgnoreCase);
        }

        [RelayCommand]
        private async Task SignUp()
        {
            if (!IsOn)
            {
                await ShowMessage("Aviso", "Sem conexão com a internet");
                return;
            }

            if (!VerifyFields())
                return;

            BtnSignUpEnabled = false;
            try
            {
                var resp = await userService.SignUpAsync(name, email, password);

                if (!resp.Success)
                {
                    ErrorMessageIsVisible = true;
                    ErrorMessage = resp.Content is ErrorTypes.EmailAlreadyExists
                        ? "Este email já está cadastrado"
                        : "Não foi possível cadastrar o usuário!";
                }
                else
                {
                    await ShowMessage("Sucesso", "Usuário cadastrado!");
                    await Shell.Current.GoToAsync("..");
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
                BtnSignUpEnabled = true;
            }
        }
    }
}
