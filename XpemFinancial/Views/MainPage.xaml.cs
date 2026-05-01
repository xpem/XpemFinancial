namespace XpemFinancial.Views
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();

            BindingContext = new VMs.MainVM();
        }

        private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
        {
            CheckBoxIncludePreviousBalance.IsChecked = !CheckBoxIncludePreviousBalance.IsChecked;
        }
    }
}
