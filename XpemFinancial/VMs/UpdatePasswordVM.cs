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
                _ = userService.RecoverPassword(Email);

                await ShowMessage("Aviso", "Email de alteração de senha enviado!");

                await Shell.Current.GoToAsync("..");
            }
        }

        public static bool ValidateEmail(string email)
        {
            return Regex.IsMatch(email, @"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z", RegexOptions.IgnoreCase);
        }
    }
}
