using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using BardsTale.UI.Settings;

namespace BardsTale.UI.Controls;

/// <summary>
/// Attached behaviour that gives a control a brief, damped horizontal "screen shake" each time
/// its <see cref="PulseProperty"/> changes — combat juice when the party takes a blow. Honours
/// the Reduced-motion accessibility setting (no shake when it's on).
/// </summary>
public sealed class Shake
{
    private Shake() { } // attached-behaviour holder; never instantiated

    /// <summary>Bump this to trigger a shake.</summary>
    public static readonly AttachedProperty<long> PulseProperty =
        AvaloniaProperty.RegisterAttached<Shake, Control, long>("Pulse");

    private static readonly AttachedProperty<long> GenProperty =
        AvaloniaProperty.RegisterAttached<Shake, Control, long>("Gen");

    public static void SetPulse(Control o, long value) => o.SetValue(PulseProperty, value);
    public static long GetPulse(Control o) => o.GetValue(PulseProperty);

    static Shake()
    {
        PulseProperty.Changed.AddClassHandler<Control>((c, _) => Animate(c));
    }

    private static async void Animate(Control c)
    {
        if (GetPulse(c) <= 0) return;                 // initial binding, not a real shake
        if (AppSettings.Current.ReducedMotion) return; // accessibility

        var gen = c.GetValue(GenProperty) + 1;
        c.SetValue(GenProperty, gen);

        var move = c.RenderTransform as TranslateTransform ?? new TranslateTransform();
        c.RenderTransform = move;

        const int frames = 16;   // ~0.27s at 16ms/frame
        const double amplitude = 7;
        for (var i = 0; i <= frames; i++)
        {
            if (c.GetValue(GenProperty) != gen) return; // a newer shake took over
            var t = i / (double)frames;
            move.X = Math.Sin(t * Math.PI * 4) * amplitude * (1 - t); // decaying oscillation
            await Task.Delay(16);
        }
        move.X = 0;
    }
}
