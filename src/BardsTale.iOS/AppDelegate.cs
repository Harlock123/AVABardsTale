using Avalonia;
using Avalonia.iOS;
using Foundation;
using BardsTale.UI;

namespace BardsTale.iOS;

// Hosts the shared App, which detects the single-view lifetime and shows MainView
// (the same root the browser and Android use). Routes sound through AVAudioPlayer.
[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        App.AudioFactory = () => new IosAudioService();
        return base.CustomizeAppBuilder(builder).WithInterFont();
    }
}
