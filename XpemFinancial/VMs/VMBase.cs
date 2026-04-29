using CommunityToolkit.Mvvm.ComponentModel;

namespace XpemFinancial.VMs
{
    public partial class VMBase : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool isBusy;

        public bool IsNotBusy => !IsBusy;
    }
}
