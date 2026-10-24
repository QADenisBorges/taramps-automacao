using Android.App;
using Android.Content;
using Android.Content.PM;
using BgsControle.App.Plataforma;

namespace BgsControle.App;

// Retrato travado (EXPERIENCE, Foundation).
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode
                           | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        PedidoLigarBluetooth.Resultado(requestCode, resultCode);
        SeletorMusicas.Resultado(requestCode, resultCode, data);
    }
}
