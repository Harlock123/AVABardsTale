using System;
using System.Collections.Generic;
using System.IO;

namespace BardsTale.UI.Audio;

/// <summary>
/// Synthesizes the game's looping background music procedurally into 16-bit PCM — no
/// audio files. Each <see cref="GameMusic"/> track is a short chord progression with a
/// sustained bass, a soft pad and an arpeggiated melody, rendered to exactly one bar-
/// aligned loop so the platform backends can repeat it seamlessly. Results are cached.
/// </summary>
public static class MusicSynth
{
    public const int SampleRate = 44100;

    private enum Wave { Sine, Triangle, Square, Saw }

    /// <summary>One track: a chord per bar, a bass note per bar, and an arpeggio pattern.</summary>
    private sealed record Track(
        double Bpm,
        int[] BassMidi,    // one bass note per bar
        int[][] Chord,     // chord tones (melody octave) per bar — also the arpeggio source
        int[] Arp,         // index into the bar's chord per eighth-note (-1 = rest)
        Wave MelWave,
        bool BassPulse,    // true: staccato root on every eighth (combat); false: sustained
        double MelGain,
        double BassGain,
        double PadGain);

    private static readonly Dictionary<GameMusic, short[]> PcmCache = new();
    private static readonly Dictionary<GameMusic, byte[]> WavCache = new();

    /// <summary>Raw 16-bit mono PCM for one loop (Android AudioTrack).</summary>
    public static short[] BuildPcm(GameMusic music)
    {
        if (PcmCache.TryGetValue(music, out var cached)) return cached;
        var pcm = Render(TrackFor(music));
        PcmCache[music] = pcm;
        return pcm;
    }

    /// <summary>The same loop wrapped in a WAV container (desktop afplay / browser / iOS).</summary>
    public static byte[] BuildWav(GameMusic music)
    {
        if (WavCache.TryGetValue(music, out var cached)) return cached;
        var wav = ToWav(BuildPcm(music));
        WavCache[music] = wav;
        return wav;
    }

    /// <summary>
    /// The loop as a WAV with its level scaled by <paramref name="gain"/> (0–1). The Windows MCI
    /// backend can't adjust a playing stream's volume, so it bakes the chosen level into the file.
    /// </summary>
    public static byte[] BuildWav(GameMusic music, double gain) =>
        gain >= 0.999 ? BuildWav(music) : ToWav(Scale(BuildPcm(music), gain));

    // Returns a volume-scaled copy of the PCM, leaving the shared cached buffer untouched.
    private static short[] Scale(short[] pcm, double gain)
    {
        gain = Math.Clamp(gain, 0, 1);
        var outp = new short[pcm.Length];
        for (var i = 0; i < pcm.Length; i++) outp[i] = (short)(pcm[i] * gain);
        return outp;
    }

    // C major: C4=60. A minor uses the same notes centred on A.
    private static Track TrackFor(GameMusic music) => music switch
    {
        // Warm major folk tune: C – Am – F – G.
        GameMusic.Town => new Track(
            Bpm: 100,
            BassMidi: new[] { 48, 45, 41, 43 },
            Chord: new[]
            {
                new[] { 72, 76, 79 }, new[] { 69, 72, 76 },
                new[] { 65, 69, 72 }, new[] { 67, 71, 74 }
            },
            Arp: new[] { 0, 1, 2, 1, 2, 1, 0, 2 },
            MelWave: Wave.Triangle, BassPulse: false,
            MelGain: 0.30, BassGain: 0.34, PadGain: 0.10),

        // Slow, sparse, ominous minor ambience for the upper crypts: Am – Dm – Am – E.
        GameMusic.Dungeon => new Track(
            Bpm: 66,
            BassMidi: new[] { 45, 50, 45, 40 },
            Chord: new[]
            {
                new[] { 69, 72, 76 }, new[] { 62, 65, 69 },
                new[] { 69, 72, 76 }, new[] { 64, 68, 71 }
            },
            Arp: new[] { 0, -1, 2, -1, 1, -1, 2, -1 },
            MelWave: Wave.Sine, BassPulse: false,
            MelGain: 0.26, BassGain: 0.32, PadGain: 0.13),

        // Darker, lower and slower for the deep crypts: Dm – Bb – Dm – A, an octave down.
        GameMusic.DungeonDeep => new Track(
            Bpm: 58,
            BassMidi: new[] { 38, 34, 38, 33 },
            Chord: new[]
            {
                new[] { 62, 65, 69 }, new[] { 58, 62, 65 },
                new[] { 62, 65, 69 }, new[] { 57, 61, 64 }
            },
            Arp: new[] { 0, -1, -1, 2, -1, 1, -1, -1 },
            MelWave: Wave.Sine, BassPulse: false,
            MelGain: 0.22, BassGain: 0.34, PadGain: 0.16),

        // Dread of the abyss: a slow chromatic descent with a tritone in the pad. Sparse and low.
        GameMusic.DungeonAbyss => new Track(
            Bpm: 50,
            BassMidi: new[] { 36, 35, 34, 33 },
            Chord: new[]
            {
                new[] { 60, 63, 66 }, new[] { 59, 62, 65 },
                new[] { 58, 61, 64 }, new[] { 57, 60, 63 }
            },
            Arp: new[] { 0, -1, -1, -1, 2, -1, -1, -1 },
            MelWave: Wave.Sine, BassPulse: false,
            MelGain: 0.20, BassGain: 0.34, PadGain: 0.18),

        // Driving minor with a pulsing bass: Am – F – G – E. Warm triangle voices
        // (not saw/square) keep the urgency without the harsh buzz.
        GameMusic.Combat => new Track(
            Bpm: 138,
            BassMidi: new[] { 45, 41, 43, 40 },
            Chord: new[]
            {
                new[] { 69, 72, 76 }, new[] { 65, 69, 72 },
                new[] { 67, 71, 74 }, new[] { 64, 68, 71 }
            },
            Arp: new[] { 0, 1, 2, 1, 0, 1, 2, 1 },
            MelWave: Wave.Triangle, BassPulse: true,
            MelGain: 0.24, BassGain: 0.30, PadGain: 0.09),

        // Bright triumphant fanfare: C – F – G – C.
        _ => new Track(
            Bpm: 120,
            BassMidi: new[] { 48, 41, 43, 48 },
            Chord: new[]
            {
                new[] { 72, 76, 79 }, new[] { 77, 81, 84 },
                new[] { 74, 79, 83 }, new[] { 72, 76, 79 }
            },
            Arp: new[] { 0, 1, 2, 1, 2, 1, 2, 2 },
            MelWave: Wave.Triangle, BassPulse: false,
            MelGain: 0.32, BassGain: 0.34, PadGain: 0.14)
    };

    private static short[] Render(Track t)
    {
        var bars = t.BassMidi.Length;
        var eighthSamples = (int)(SampleRate * (60.0 / t.Bpm / 2.0));
        var barSamples = eighthSamples * 8;
        var total = barSamples * bars;
        var buf = new double[total];

        for (var bar = 0; bar < bars; bar++)
        {
            var barStart = bar * barSamples;
            var chord = t.Chord[bar];

            // Bass — a sustained root, or a warm staccato pulse on each eighth.
            if (t.BassPulse)
            {
                for (var e = 0; e < 8; e++)
                    AddNote(buf, barStart + e * eighthSamples, (int)(eighthSamples * 0.5),
                        Freq(t.BassMidi[bar]), Wave.Triangle, t.BassGain);
            }
            else
            {
                AddNote(buf, barStart, barSamples, Freq(t.BassMidi[bar]), Wave.Sine, t.BassGain);
            }

            // Pad — the chord held softly under everything.
            foreach (var note in chord)
                AddNote(buf, barStart, barSamples, Freq(note), Wave.Sine, t.PadGain / chord.Length);

            // Melody — an arpeggio walking the chord tones.
            for (var e = 0; e < 8; e++)
            {
                var idx = t.Arp[e % t.Arp.Length];
                if (idx < 0) continue; // rest
                var note = chord[idx % chord.Length];
                AddNote(buf, barStart + e * eighthSamples, (int)(eighthSamples * 0.9),
                    Freq(note), t.MelWave, t.MelGain);
            }
        }

        return Finish(buf);
    }

    private static double Freq(int midi) => 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);

    // Adds one enveloped note into the mix buffer (quick attack, smooth release → click-free loop).
    private static void AddNote(double[] buf, int start, int len, double freq, Wave wave, double gain)
    {
        if (len <= 0 || start >= buf.Length) return;
        var attack = Math.Min(len / 4, (int)(0.012 * SampleRate));
        var release = Math.Min(len / 2, (int)(0.09 * SampleRate));
        double phase = 0;
        for (var i = 0; i < len; i++)
        {
            var idx = start + i;
            if (idx >= buf.Length) break;
            phase += 2 * Math.PI * freq / SampleRate;
            var w = wave switch
            {
                Wave.Sine => Math.Sin(phase),
                Wave.Triangle => 2.0 / Math.PI * Math.Asin(Math.Sin(phase)),
                Wave.Square => Math.Sin(phase) >= 0 ? 1.0 : -1.0,
                _ => 2.0 * (phase / (2 * Math.PI) % 1.0) - 1.0
            };
            double env = 1.0;
            if (i < attack) env = i / (double)attack;
            else if (i > len - release) env = (len - i) / (double)release;
            buf[idx] += w * gain * env;
        }
    }

    // Normalize to avoid clipping, add a short fade at both ends so the loop point is silent.
    private static short[] Finish(double[] buf)
    {
        double peak = 0;
        foreach (var v in buf) peak = Math.Max(peak, Math.Abs(v));
        var scale = peak > 0.92 ? 0.92 / peak : 1.0;

        var fade = (int)(0.006 * SampleRate);
        var pcm = new short[buf.Length];
        for (var i = 0; i < buf.Length; i++)
        {
            var g = scale;
            if (i < fade) g *= i / (double)fade;
            else if (i > buf.Length - fade) g *= (buf.Length - i) / (double)fade;
            pcm[i] = (short)Math.Clamp(buf[i] * g * short.MaxValue, short.MinValue, short.MaxValue);
        }
        return pcm;
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
        w.Write(16);
        w.Write((short)1);   // PCM
        w.Write((short)1);   // mono
        w.Write(SampleRate);
        w.Write(SampleRate * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write("data"u8.ToArray());
        w.Write(dataBytes);
        foreach (var s in samples) w.Write(s);
        w.Flush();
        return ms.ToArray();
    }
}
