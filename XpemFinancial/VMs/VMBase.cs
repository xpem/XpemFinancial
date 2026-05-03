using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XpemFinancial.VMs
{
    public partial class VMBase : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool isBusy;

        public bool IsNotBusy => !IsBusy;

        public static async Task ShowMessage(string message, string title)
        {
            if (DeviceInfo.Platform == DevicePlatform.iOS || DeviceInfo.Platform == DevicePlatform.Android)
            {
                ToastDuration duration = ToastDuration.Short;

                var toast = Toast.Make(message, duration, 15);
                await toast.Show();
            }
            else
                await Application.Current.Windows[0].Page.DisplayAlertAsync(title, message, null, "Continuar");
        }
    }
}
