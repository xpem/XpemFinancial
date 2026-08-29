using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Service;
using System.Text.RegularExpressions;

namespace XpemFinancial.VMs
{
    public partial class UpdatePasswordVM(IUserService userService) : VMBase
    {
        [ObservableProperty] private string email;
        [ObservableProperty] private bool isRequired;

        [RelayCommand]
        private async Task UpdatePassword()
        {
            if (!(Connectivity.NetworkAccess == NetworkAccess.Internet))
            {
                await ShowMessage("Aviso", "Sem conexão com a internet");
                IsRequired = true;
                return;
            }

            if (string.IsNullOrEmpty(Email))
            {
                await ShowMessage("Aviso", "Digite um email válido");
                IsRequired = true;
                return;
            }
            else if (!ValidateEmail(Email))
            {
                await ShowMessage("Aviso", "Digite um email válido");
                IsRequired = true;
                return;
            }
            else
            {
                IsBusy = true;
                try
                {
                    var (success, message) = await userService.RecoverPassword(Email);

                    if (success)
                    {
                        await ShowMessage("Aviso", "Email de alteração de senha enviado!");
                        await Shell.Current.GoToAsync("..");
                    }
                    else
                    {
                        App.WriteCrashLog("RecoverPassword.Failed", $"Email: {Email}, Motivo: {message}");
                        await ShowMessage("Erro", "Não foi possível enviar o email. Verifique se o email está cadastrado.");
                    }
                }
                catch (Exception ex)
                {
                    App.WriteCrashLog("RecoverPassword.Exception", $"Email: {Email}{Environment.NewLine}{ex}");
                    await ShowMessage("Erro", "Erro ao conectar com o servidor. Tente novamente.");
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        public static bool ValidateEmail(string email)
        {
            return Regex.IsMatch(email, @"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z", RegexOptions.IgnoreCase);
        }
    }
}
