using System.Runtime.Versioning;
using Android.App;

using Android.OS;


namespace MauiApp1;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Регистрация Bluetooth-приёмников (ACTION_FOUND, ACTION_BOND_STATE_CHANGED и др.)
        // — см. раздел 8.2 ТЗ. Приёмники живут всё время работы Activity.
        Services.BtReceivers.Register(this);
    }
}