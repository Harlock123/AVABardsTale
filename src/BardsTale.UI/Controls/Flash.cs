using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using BardsTale.UI.Settings;

namespace BardsTale.UI.Controls;

/// <summary>
/// Attached behaviour for a hit-flash overlay: each time <see cref="PulseProperty"/> changes, the
/// control flashes (opacity peaks then fades to nothing), reading as an impact when a foe is struck.
/// Honours the Reduced-motion accessibility setting (no flash when it's on).
/// </summary>
public sealed class Flash
{
    private Flash() { } // attached-behaviour holder; never instantiated

    /// <summary>Bump this to trigger a flash.</summary>
    public static readonly AttachedProperty<long> PulseProperty =
        AvaloniaProperty.RegisterAttached<Flash, Control, long>("Pulse");

    private static readonly AttachedProperty<long> GenProperty =
        AvaloniaProperty.RegisterAttached<Flash, Control, long>("Gen");

    public static void SetPulse(Control o, long value) => o.SetValue(PulseProperty, value);
    public static long GetPulse(Control o) => o.GetValue(PulseProperty);

    static Flash()
    {
        PulseProperty.Changed.AddClassHandler<Control>((c, _) => Animate(c));
    }

    private static async void Animate(Control c)
    {
        if (GetPulse(c) <= 0) { c.Opacity = 0; return; } // initial binding, not a real flash
        if (AppSettings.Current.ReducedMotion) { c.Opacity = 0; return; }

        var gen = c.GetValue(GenProperty) + 1;
        c.SetValue(GenProperty, gen);

        const int frames = 14; // ~0.22s
        const double peak = 0.5;
        for (var i = 0; i <= frames; i++)
        {
            if (c.GetValue(GenProperty) != gen) return; // a newer flash took over
            c.Opacity = peak * (1 - i / (double)frames); // fade from peak to 0
            await Task.Delay(16);
        }
        c.Opacity = 0;
    }
}
