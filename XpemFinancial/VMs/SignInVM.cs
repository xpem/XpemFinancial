using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XpemFinancial.VMs
{
    public partial class SignInVM : VMBase
    {
        [ObservableProperty] private string email;
        [ObservableProperty] private string password;
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
                await Application.Current.Windows[0].Page.DisplayAlert("Aviso", "É necessário ter acesso a internet para efetuar o primeiro acesso.", null, "Ok");
                isValid = false;
            }
            if (Password.Length <= 3)
            {
                await Application.Current.Windows[0].Page.DisplayAlert("Aviso", "Digite sua senha", null, "Ok");
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
                {
                    return;
                }

                SignInText = "Acessando...";
                BtnSignEnabled = false;

                //Models.Responses.BLLResponse resp = await userBLL.SignIn(Email, Password);

                //if (resp.Success)
                //{
                //    if (resp.Content is not null and int)
                //        ((App)App.Current).Uid = (int)resp.Content;

                //    await Shell.Current.GoToAsync($"{nameof(FirstSyncProcess)}", false);

                //    //Application.Current.MainPage = new NavigationPage();
                //    //_ = (Application.Current.MainPage.Navigation).PushAsync(navigation.ResolvePage<Main>(), true);
                //}
                //else
                //{
                //    string errorMessage = "";

                //    if (resp.Error == Models.Responses.ErrorTypes.WrongEmailOrPassword)
                //        errorMessage = "Email/senha incorretos";
                //    else if (resp.Error == Models.Responses.ErrorTypes.ServerUnavaliable)
                //        errorMessage = "Servidor indisponível, favor entrar em contato com o desenvolvedor.";
                //    else errorMessage = "Erro não mapeado, favor entrar em contato com o desenvolvedor.";

                //    await Application.Current.Windows[0].Page.DisplayAlert("Aviso", errorMessage, null, "Ok");
                //}

                BtnSignEnabled = true;
                SignInText = "Acessar";
                IsBusy = false;
            }
            catch { throw; }

        }

        [RelayCommand]
        private async Task CreateUser() => throw new NotImplementedException();// await Shell.Current.GoToAsync($"{nameof(SignUp)}");

        [RelayCommand]
        private async Task UpdatePassword() => await Shell.Current.GoToAsync($"{nameof(UpdatePassword)}");


    }
}
