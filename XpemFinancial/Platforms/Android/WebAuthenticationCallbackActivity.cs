using Android.App;
using Android.Content.PM;

namespace XpemFinancial.Platforms.Android
{
    [Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
    [IntentFilter(
        [global::Android.Content.Intent.ActionView],
        Categories = [global::Android.Content.Intent.CategoryDefault, global::Android.Content.Intent.CategoryBrowsable],
        DataScheme = CallbackScheme)]
    public class WebAuthenticationCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
    {
        // Scheme do deep link que o servidor usa para devolver o token ao app
        private const string CallbackScheme = "com.xpem.xpemfinancial";
    }
}
