using System;
using System.Threading;

namespace BardsTale.UI.Audio;

/// <summary>
/// Performs a smooth <em>dip crossfade</em> between looping tracks on a single-stream backend:
/// it fades the current track down to silence, swaps in the new track, then fades that up to the
/// target volume. Single-stream backends can't overlap two tracks, so the fade passes briefly
/// through silence rather than blending — which sounds clean and avoids two themes clashing.
///
/// Requires a backend whose <see cref="IMusicService.SetVolume"/> takes effect live (see
/// <see cref="IMusicService.SupportsLiveVolume"/>). The sleep delegate is injectable so the ramp
/// can be driven synchronously, with no real delay, in tests.
/// </summary>
public sealed class MusicCrossfader
{
    private readonly IMusicService _svc;
    private readonly int _steps;
    private readonly int _stepMs;
    private readonly Action<int> _sleep;

    public MusicCrossfader(IMusicService svc, int fadeMs = 700, int steps = 14, Action<int>? sleep = null)
    {
        _svc = svc;
        _steps = Math.Max(1, steps);
        _stepMs = Math.Max(0, fadeMs) / 2 / _steps; // fadeMs split across a fade-out and a fade-in half
        _sleep = sleep ?? Thread.Sleep;
    }

    /// <summary>
    /// Runs the crossfade to <paramref name="to"/>, settling at <paramref name="target"/> volume.
    /// When <paramref name="fadeOutFirst"/> is false (starting from silence) the fade-out half is
    /// skipped. The token aborts the ramp promptly; a clean finish leaves the new track at target.
    /// </summary>
    public void Run(GameMusic to, double target, bool fadeOutFirst, CancellationToken ct)
    {
        target = Math.Clamp(target, 0, 1);

        if (fadeOutFirst)
        {
            for (var i = 1; i <= _steps; i++)
            {
                if (ct.IsCancellationRequested) return;
                _svc.SetVolume(target * (1.0 - (double)i / _steps));
                _sleep(_stepMs);
            }
        }
        if (ct.IsCancellationRequested) return;

        // Swap tracks at silence, then bring the new one up.
        _svc.Play(to);
        _svc.SetVolume(0);

        for (var i = 1; i <= _steps; i++)
        {
            if (ct.IsCancellationRequested) return;
            _svc.SetVolume(target * ((double)i / _steps)); // group the fraction so it never overshoots target
            _sleep(_stepMs);
        }
        _svc.SetVolume(target);
    }
}
