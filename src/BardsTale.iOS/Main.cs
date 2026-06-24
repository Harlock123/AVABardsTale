using UIKit;

namespace BardsTale.iOS;

public static class Application
{
    // The iOS entry point; UIApplicationMain hands control to AppDelegate.
    private static void Main(string[] args)
    {
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
