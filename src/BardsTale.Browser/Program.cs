using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using BardsTale.UI;

[assembly: SupportedOSPlatform("browser")]

internal sealed partial class Program
{
    // Entry point for the WebAssembly head. Hosts the shared App in the browser's
    // single-view lifetime, attaching to the <div id="out"> element in index.html.
    private static Task Main(string[] args) => BuildAvaloniaApp()
        .WithInterFont()
        .StartBrowserAppAsync("out");

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
