using XpemFinancial.VMs;

namespace XpemFinancial
{
    public partial class AppShell : Shell
    {
        public AppShell(AppShellVM appShellVM)
        {
            InitializeComponent();

            BindingContext = appShellVM;

            if (this.FlyoutHeader is BindableObject flyoutHeader)
            {
                flyoutHeader.BindingContext = appShellVM;
            }
        }
    }
}
