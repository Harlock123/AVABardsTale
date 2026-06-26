using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace BardsTale.UI.Controls;

/// <summary>
/// Attached behaviour that makes a <see cref="TextBlock"/> "pop" — float upward while
/// fading out — each time its <see cref="PulseProperty"/> changes. The view models bump
/// the pulse (and set the block's text/colour) when a target takes damage or is healed,
/// giving the classic floating-combat-number flourish without touching the game engine.
/// </summary>
public sealed class FloatingNumber
{
    private FloatingNumber() { } // attached-behaviour holder; never instantiated

    /// <summary>Bump this (e.g. bind to an ever-incrementing counter) to replay the pop.</summary>
    public static readonly AttachedProperty<long> PulseProperty =
        AvaloniaProperty.RegisterAttached<FloatingNumber, TextBlock, long>("Pulse");

    // A per-block generation guard so a fresh pop supersedes one still animating.
    private static readonly AttachedProperty<long> GenProperty =
        AvaloniaProperty.RegisterAttached<FloatingNumber, TextBlock, long>("Gen");

    public static void SetPulse(TextBlock o, long value) => o.SetValue(PulseProperty, value);
    public static long GetPulse(TextBlock o) => o.GetValue(PulseProperty);

    static FloatingNumber()
    {
        PulseProperty.Changed.AddClassHandler<TextBlock>((tb, _) => Animate(tb));
    }

    private static async void Animate(TextBlock tb)
    {
        if (GetPulse(tb) <= 0) { tb.Opacity = 0; return; } // initial binding, not a real pop

        var gen = tb.GetValue(GenProperty) + 1;
        tb.SetValue(GenProperty, gen);

        var move = new TranslateTransform();
        tb.RenderTransform = move;
        tb.Opacity = 1;

        const int frames = 48; // ~0.8s at 16ms/frame
        for (var i = 0; i <= frames; i++)
        {
            if (tb.GetValue(GenProperty) != gen) return; // a newer pop took over
            var t = i / (double)frames;
            move.Y = -42 * (1 - (1 - t) * (1 - t)); // ease-out drift upward
            tb.Opacity = 1 - t;                      // linear fade
            await Task.Delay(16);
        }
        tb.Opacity = 0;
    }
}
