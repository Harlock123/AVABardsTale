using System;
using System.Collections.Generic;
using System.IO;

namespace BardsTale.UI.Audio;

/// <summary>
/// Synthesizes the game's sound effects procedurally into 16-bit PCM WAV bytes — no
/// audio asset files needed. Each <see cref="GameSound"/> is a short sequence of tone
/// segments with a simple attack/decay envelope. Results are cached.
/// </summary>
public static class ToneSynth
{
    public const int SampleRate = 44100;

    private enum Wave { Sine, Square, Saw, Noise }

    private readonly record struct Seg(double FreqStart, double FreqEnd, int Ms, Wave Wave, double Gain);

    private static readonly Dictionary<GameSound, short[]> PcmCache = new();
    private static readonly Dictionary<GameSound, byte[]> WavCache = new();
    private static readonly Random Noise = new(20251); // fixed seed → reproducible noise

    /// <summary>Raw 16-bit mono PCM samples (for Android's AudioTrack).</summary>
    public static short[] BuildPcm(GameSound sound)
    {
        if (PcmCache.TryGetValue(sound, out var cached)) return cached;
        var pcm = Render(RecipeFor(sound));
        PcmCache[sound] = pcm;
        return pcm;
    }

    /// <summary>The same audio wrapped in a WAV container (for desktop afplay / browser decode).</summary>
    public static byte[] BuildWav(GameSound sound)
    {
        if (WavCache.TryGetValue(sound, out var cached)) return cached;
        var wav = ToWav(BuildPcm(sound));
        WavCache[sound] = wav;
        return wav;
    }

    /// <summary>
    /// The effect as a WAV with its level scaled by <paramref name="gain"/> (0–1). The Windows MCI
    /// backend can't adjust a playing stream's volume, so it bakes the chosen level into the file.
    /// </summary>
    public static byte[] BuildWav(GameSound sound, double gain) =>
        gain >= 0.999 ? BuildWav(sound) : ToWav(Scale(BuildPcm(sound), gain));

    // Returns a volume-scaled copy of the PCM, leaving the shared cached buffer untouched.
    private static short[] Scale(short[] pcm, double gain)
    {
        gain = Math.Clamp(gain, 0, 1);
        var outp = new short[pcm.Length];
        for (var i = 0; i < pcm.Length; i++) outp[i] = (short)(pcm[i] * gain);
        return outp;
    }

    private static Seg[] RecipeFor(GameSound sound) => sound switch
    {
        GameSound.UiConfirm => new[] { new Seg(680, 920, 70, Wave.Sine, 0.40) },
        GameSound.Door => new[] { new Seg(170, 80, 200, Wave.Sine, 0.7), new Seg(0, 0, 70, Wave.Noise, 0.18) },
        GameSound.Attack => new[] { new Seg(0, 0, 70, Wave.Noise, 0.5), new Seg(220, 110, 90, Wave.Saw, 0.4) },
        GameSound.SpellCast => new[] { new Seg(420, 1200, 280, Wave.Sine, 0.4), new Seg(640, 1500, 280, Wave.Sine, 0.18) },
        GameSound.Hurt => new[] { new Seg(320, 120, 220, Wave.Saw, 0.5) },
        GameSound.EnemyDefeated => new[] { new Seg(240, 60, 320, Wave.Saw, 0.5), new Seg(0, 0, 120, Wave.Noise, 0.18) },
        GameSound.Coin => new[] { new Seg(1000, 1000, 60, Wave.Square, 0.32), new Seg(1500, 1500, 90, Wave.Square, 0.32) },
        GameSound.LevelUp => new[]
        {
            new Seg(523, 523, 90, Wave.Sine, 0.4), new Seg(659, 659, 90, Wave.Sine, 0.4),
            new Seg(784, 784, 150, Wave.Sine, 0.45)
        },
        GameSound.Victory => new[]
        {
            new Seg(523, 523, 110, Wave.Sine, 0.42), new Seg(659, 659, 110, Wave.Sine, 0.42),
            new Seg(784, 784, 110, Wave.Sine, 0.42), new Seg(1046, 1046, 320, Wave.Sine, 0.5)
        },
        GameSound.Defeat => new[]
        {
            new Seg(392, 392, 200, Wave.Saw, 0.42), new Seg(330, 330, 200, Wave.Saw, 0.42),
            new Seg(262, 196, 360, Wave.Saw, 0.45)
        },

        // --- exploration ---
        GameSound.FootstepStone => new[] { new Seg(0, 0, 35, Wave.Noise, 0.22), new Seg(130, 80, 55, Wave.Sine, 0.28) },
        GameSound.FootstepDungeon => new[] { new Seg(95, 60, 90, Wave.Sine, 0.3), new Seg(0, 0, 45, Wave.Noise, 0.16) },
        GameSound.StairsDown => new[] { new Seg(520, 150, 360, Wave.Sine, 0.4) },
        GameSound.StairsUp => new[] { new Seg(180, 540, 360, Wave.Sine, 0.4) },

        // --- town services ---
        GameSound.Heal => new[] { new Seg(660, 660, 110, Wave.Sine, 0.4), new Seg(990, 990, 220, Wave.Sine, 0.45) },
        GameSound.Buy => new[] { new Seg(900, 900, 60, Wave.Square, 0.3), new Seg(1300, 1300, 110, Wave.Square, 0.34) },
        GameSound.Sell => new[] { new Seg(1300, 1300, 60, Wave.Square, 0.3), new Seg(900, 900, 110, Wave.Square, 0.32) },
        GameSound.Equip => new[] { new Seg(0, 0, 25, Wave.Noise, 0.28), new Seg(1500, 1050, 90, Wave.Square, 0.3) },

        // --- spell flavours ---
        GameSound.SpellFire => new[] { new Seg(200, 900, 180, Wave.Saw, 0.4), new Seg(0, 0, 150, Wave.Noise, 0.28) },
        GameSound.SpellBuff => new[] { new Seg(300, 820, 300, Wave.Sine, 0.36), new Seg(450, 1120, 300, Wave.Sine, 0.18) },

        _ => new[] { new Seg(600, 600, 80, Wave.Sine, 0.3) }
    };

    private static short[] Render(Seg[] segs)
    {
        var samples = new List<short>();
        foreach (var seg in segs)
        {
            var count = SampleRate * seg.Ms / 1000;
            double phase = 0;
            for (var i = 0; i < count; i++)
            {
                var t = i / (double)count;
                var freq = seg.FreqStart + (seg.FreqEnd - seg.FreqStart) * t;
                phase += 2 * Math.PI * freq / SampleRate;

                var w = seg.Wave switch
                {
                    Wave.Sine => Math.Sin(phase),
                    Wave.Square => Math.Sin(phase) >= 0 ? 1.0 : -1.0,
                    Wave.Saw => 2.0 * (phase / (2 * Math.PI) % 1.0) - 1.0,
                    _ => Noise.NextDouble() * 2 - 1
                };

                // Quick attack, then a smooth decay to silence over the segment.
                var env = Math.Min(1.0, t / 0.02) * (1.0 - t) * (1.0 - t);
                samples.Add((short)Math.Clamp(w * seg.Gain * env * short.MaxValue, short.MinValue, short.MaxValue));
            }
        }
        return samples.ToArray();
    }

    private static byte[] ToWav(short[] samples)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        var dataBytes = samples.Length * 2;
        w.Write("RIFF"u8.ToArray());
        w.Write(36 + dataBytes);
        w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray());
        w.Write(16);                 // fmt chunk size
        w.Write((short)1);           // PCM
        w.Write((short)1);           // mono
        w.Write(SampleRate);
        w.Write(SampleRate * 2);     // byte rate
        w.Write((short)2);           // block align
        w.Write((short)16);          // bits per sample
        w.Write("data"u8.ToArray());
        w.Write(dataBytes);
        foreach (var s in samples) w.Write(s);
        w.Flush();
        return ms.ToArray();
    }
}
