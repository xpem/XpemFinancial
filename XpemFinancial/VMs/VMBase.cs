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

        /// <summary>
        /// False while the window is still starting up (or being swapped). On Windows a dialog opened in
        /// that moment throws "This element does not have a XamlRoot" from inside MAUI's async-void alert
        /// handler, which cannot be caught by the caller and takes the whole process down.
        /// </summary>
        public static bool CanShowAlert()
        {
#if WINDOWS
            var window = Application.Current?.Windows.Count > 0 ? Application.Current.Windows[0] : null;
            var native = window?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;

            return window?.Page is not null && native?.Content?.XamlRoot is not null;
#else
            return true;
#endif
        }

        public static async Task ShowMessage(string title, string message)
        {
#if WINDOWS
            var window = Application.Current?.Windows.Count > 0 ? Application.Current.Windows[0] : null;
            if (window?.Page is not null && CanShowAlert())
                await window.Page.DisplayAlertAsync(title, message, "OK");
            else
                System.Diagnostics.Debug.WriteLine($"[ShowMessage] Skipped (window not ready): {title} - {message}");
#else
            var snackbar = Snackbar.Make(message, duration: TimeSpan.FromSeconds(3));
            await snackbar.Show();
#endif
        }
    }
}
