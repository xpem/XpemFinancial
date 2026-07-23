using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XpemFinancial.VMs
{
    public partial class VMBase : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool isBusy;

        public bool IsNotBusy => !IsBusy;

        protected static bool IsOn => Connectivity.NetworkAccess == NetworkAccess.Internet;

        public static async Task ShowMessage(string title, string message)
        {
#if WINDOWS
            if (Application.Current?.MainPage is not null)
                await Application.Current.MainPage.DisplayAlert(title, message, "OK");
#else
            var snackbar = Snackbar.Make(message, duration: TimeSpan.FromSeconds(3));
            await snackbar.Show();
#endif
        }
    }
}
