using System;
using BardsTale.UI.Audio;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// The Windows MCI backends can't change a playing stream's volume, so they bake the chosen
/// level into the WAV via the gain-aware <c>BuildWav</c> overloads. These guard that the gain
/// actually scales the samples (so the volume sliders work) and that full gain is a no-op.
/// </summary>
public class AudioGainTests
{
    // Largest absolute 16-bit sample in a mono PCM WAV (data starts at byte 44).
    private static int Peak(byte[] wav)
    {
        var peak = 0;
        for (var i = 44; i + 1 < wav.Length; i += 2)
            peak = Math.Max(peak, Math.Abs(BitConverter.ToInt16(wav, i)));
        return peak;
    }

    [Fact]
    public void Music_gain_scales_amplitude_proportionally()
    {
        var full = Peak(MusicSynth.BuildWav(GameMusic.Town, 1.0));
        var half = Peak(MusicSynth.BuildWav(GameMusic.Town, 0.5));
        var quarter = Peak(MusicSynth.BuildWav(GameMusic.Town, 0.25));

        Assert.True(full > 2000, "full-volume loop should be clearly audible");
        Assert.InRange(half, full * 0.5 - 2, full * 0.5 + 2);
        Assert.InRange(quarter, full * 0.25 - 2, full * 0.25 + 2);
    }

    [Fact]
    public void Sfx_gain_scales_amplitude_proportionally()
    {
        var full = Peak(ToneSynth.BuildWav(GameSound.Coin, 1.0));
        var half = Peak(ToneSynth.BuildWav(GameSound.Coin, 0.5));

        Assert.True(full > 2000, "full-volume effect should be clearly audible");
        Assert.InRange(half, full * 0.5 - 2, full * 0.5 + 2);
    }

    [Fact]
    public void Full_gain_matches_the_unscaled_wav()
    {
        Assert.Equal(MusicSynth.BuildWav(GameMusic.Combat), MusicSynth.BuildWav(GameMusic.Combat, 1.0));
        Assert.Equal(ToneSynth.BuildWav(GameSound.Door), ToneSynth.BuildWav(GameSound.Door, 1.0));
    }

    [Fact]
    public void Zero_gain_is_silent()
    {
        Assert.Equal(0, Peak(MusicSynth.BuildWav(GameMusic.Town, 0.0)));
        Assert.Equal(0, Peak(ToneSynth.BuildWav(GameSound.Coin, 0.0)));
    }
}
