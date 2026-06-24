using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using BardsTale.Browser;
using BardsTale.UI;

[assembly: SupportedOSPlatform("browser")]

internal sealed partial class Program
{
    // Entry point for the WebAssembly head. Hosts the shared App in the browser's
    // single-view lifetime, attaching to the <div id="out"> element in index.html.
    private static async Task Main(string[] args)
    {
        // Load the IndexedDB module and route all saves through it (persistent,
        // per-browser/per-origin). Must run before the App creates its view model.
        // Module URL is resolved relative to the runtime's _framework/ folder, so
        // step up one level to reach saveStore.js at the app root.
        await JSHost.ImportAsync("saveStore", "../saveStore.js");
        App.SaveStoreFactory = () => new IndexedDbSaveStore();
        await IndexedDbSaveStore.RequestPersistentStorageAsync();

        // Route sound through the Web Audio backend.
        await JSHost.ImportAsync("audio", "../audio.js");
        App.AudioFactory = () => new BrowserAudioService();

        // Route looping background music through its own Web Audio backend.
        await JSHost.ImportAsync("music", "../music.js");
        App.MusicFactory = () => new BrowserMusicService();

        await BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
