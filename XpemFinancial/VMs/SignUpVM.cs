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
            bool isValid = true;

            if (string.IsNullOrEmpty(Name) || string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password) || string.IsNullOrEmpty(ConfirmPassword))
            {
                ErrorMessageIsVisible = true;
                ErrorMessage = "Preencha todos os campos obrigatórios";
                isValid = false;
            }

            if (!ValidateEmail(Email))
            {
                ErrorMessageIsVisible = true;
                ErrorMessage = "Digite um email válido";
                isValid = false;
            }

            if (Password.Length < 4)
            {
                ErrorMessageIsVisible = true;
                ErrorMessage = "A senha deve ter pelo menos 4 caracteres";
                isValid =  false;
            }

            if (!ConfirmPassword.Equals(Password, StringComparison.Ordinal))
            {
                ErrorMessageIsVisible = true;
                ErrorMessage = "As senhas não coincidem";
                isValid = false;
            }

            if (!isValid)
                IsRequired = true;
            else
                IsRequired = false;

            return isValid;
        }

        public static bool ValidateEmail(string email)
        {
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
                BtnSignUpEnabled = true;
            }
        }
    }
}
