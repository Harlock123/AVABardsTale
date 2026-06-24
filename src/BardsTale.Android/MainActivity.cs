using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using BardsTale.UI;

namespace BardsTale.Android;

// The single launcher activity. AvaloniaMainActivity hosts the shared App, which
// detects the single-view lifetime and shows MainView (the same root the browser uses).
[Activity(
    Label = "The Bard's Tale",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    // The UI is laid out for a wide (landscape) screen, so lock to landscape.
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        App.AudioFactory = () => new AndroidAudioService();
        return base.CustomizeAppBuilder(builder).WithInterFont();
    }
}
