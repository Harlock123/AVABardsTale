using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BardsTale.UI.Audio;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Covers the music dip-crossfade: it fades the current track out to silence, swaps in the new
/// track, and fades that up to the target volume — driven synchronously here via a no-op sleep.
/// </summary>
public class MusicCrossfadeTests
{
    /// <summary>An <see cref="IMusicService"/> that records the exact sequence of backend calls.</summary>
    private sealed class RecordingMusic : IMusicService
    {
        public readonly List<(string Op, double Vol, GameMusic? Track)> Ops = new();
        public void Play(GameMusic track) => Ops.Add(("play", 0, track));
        public void Stop() => Ops.Add(("stop", 0, null));
        public void SetVolume(double volume) => Ops.Add(("vol", volume, null));
        public bool SupportsLiveVolume => true;

        public IEnumerable<double> Volumes => Ops.Where(o => o.Op == "vol").Select(o => o.Vol);
        public int PlayIndex => Ops.FindIndex(o => o.Op == "play");
    }

    private static MusicCrossfader Fader(RecordingMusic svc) =>
        new(svc, fadeMs: 0, steps: 6, sleep: _ => { }); // no real delay; 6 ramp steps per half

    [Fact]
    public void A_crossfade_fades_out_swaps_then_fades_in_to_target()
    {
        var svc = new RecordingMusic();
        Fader(svc).Run(GameMusic.Combat, target: 0.8, fadeOutFirst: true, CancellationToken.None);

        // Exactly one track swap, to the requested track.
        Assert.Single(svc.Ops, o => o.Op == "play");
        Assert.Equal(GameMusic.Combat, svc.Ops.First(o => o.Op == "play").Track);

        // The fade-out runs before the swap; the swap lands at silence.
        Assert.True(svc.PlayIndex > 0, "the fade-out should precede the track swap");
        Assert.True(svc.Ops[svc.PlayIndex - 1].Vol <= 0.2, "volume should be near zero just before the swap");
        Assert.Equal(0, svc.Ops[svc.PlayIndex + 1].Vol); // first thing after the swap is silence

        // It settles exactly at the target, and never strays outside [0, target].
        Assert.Equal(0.8, svc.Ops.Last(o => o.Op == "vol").Vol);
        Assert.All(svc.Volumes, v => Assert.InRange(v, 0.0, 0.8));
    }

    [Fact]
    public void Starting_from_silence_skips_the_fade_out()
    {
        var svc = new RecordingMusic();
        Fader(svc).Run(GameMusic.Town, target: 0.5, fadeOutFirst: false, CancellationToken.None);

        // No fade-out: the very first operation is the track swap itself.
        Assert.Equal("play", svc.Ops[0].Op);
        Assert.Equal(GameMusic.Town, svc.Ops[0].Track);
        Assert.Equal(0.5, svc.Ops.Last(o => o.Op == "vol").Vol); // still ramps up to target
    }

    [Fact]
    public void A_cancelled_crossfade_does_nothing()
    {
        var svc = new RecordingMusic();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Fader(svc).Run(GameMusic.Combat, target: 0.8, fadeOutFirst: true, cts.Token);

        Assert.Empty(svc.Ops); // aborts before touching the backend
    }

    [Fact]
    public void The_fade_in_volumes_rise_monotonically()
    {
        var svc = new RecordingMusic();
        Fader(svc).Run(GameMusic.Dungeon, target: 1.0, fadeOutFirst: false, CancellationToken.None);

        // After the swap + the silence reset, the volumes only climb.
        var rising = svc.Ops.Skip(svc.PlayIndex + 1).Where(o => o.Op == "vol").Select(o => o.Vol).ToList();
        for (var i = 1; i < rising.Count; i++)
            Assert.True(rising[i] >= rising[i - 1], "fade-in volumes should not dip");
        Assert.Equal(1.0, rising.Last());
    }
}
