using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XpemFinancial.VMs
{
    public partial class VMBase : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        public partial bool IsBusy { get; set; }

        public bool IsNotBusy => !IsBusy;

        protected static bool IsOn => Connectivity.NetworkAccess == NetworkAccess.Internet;

        public static async Task ShowMessage(string title, string message)
        {
#if WINDOWS
            var window = Application.Current?.Windows.Count > 0 ? Application.Current.Windows[0] : null;
            if (window?.Page is not null)
                await window.Page.DisplayAlertAsync(title, message, "OK");
#else
            var snackbar = Snackbar.Make(message, duration: TimeSpan.FromSeconds(3));
            await snackbar.Show();
#endif
        }
    }
}
