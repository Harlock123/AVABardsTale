using Avalonia;
using System;
using BardsTale.UI;
using BardsTale.UI.Audio;

namespace BardsTale.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // In screenshot-capture mode (BT_SHOT), run silent so the batch doesn't fire sound effects.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BT_SHOT")))
        {
            App.AudioFactory = () => new NullAudioService();
            App.MusicFactory = () => new NullMusicService();
        }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
