using System;
using System.Diagnostics;
using System.Globalization;

namespace BardsTale.UI.Audio;

/// <summary>
/// Detects a usable command-line WAV player on Linux and builds its arguments. Linux has no
/// built-in audio API, so — like macOS's <c>afplay</c> — the desktop backend shells out to
/// whatever player is installed, preferring ones that can loop natively (ffplay, mpv) and
/// falling back to the near-universal PulseAudio/ALSA tools (paplay, aplay).
/// </summary>
internal static class LinuxPlayer
{
    /// <summary>One detected player: its command and how to build a one-shot / looping invocation.</summary>
    internal sealed record Player(string Command, bool CanLoop,
        Func<string, double, string> OneShotArgs, Func<string, double, string>? LoopArgs);

    private static Player? _cached;
    private static bool _probed;

    /// <summary>The best available player, or null if none is installed.</summary>
    internal static Player? Detect()
    {
        if (_probed) return _cached;
        _probed = true;

        foreach (var p in Candidates)
            if (Exists(p.Command)) { _cached = p; break; }

        return _cached;
    }

    private static readonly Player[] Candidates =
    {
        // ffplay (ffmpeg): one-shot and native loop, with volume 0–100.
        new("ffplay", true,
            (f, v) => $"-nodisp -autoexit -loglevel quiet -volume {Pct(v)} \"{f}\"",
            (f, v) => $"-nodisp -autoexit -loglevel quiet -loop 0 -volume {Pct(v)} \"{f}\""),

        // mpv: one-shot and native loop, with volume 0–100.
        new("mpv", true,
            (f, v) => $"--no-video --really-quiet --volume={Pct(v)} \"{f}\"",
            (f, v) => $"--no-video --really-quiet --loop=inf --volume={Pct(v)} \"{f}\""),

        // paplay (PulseAudio/PipeWire): one-shot only, volume 0–65536.
        new("paplay", false,
            (f, v) => $"--volume={(int)(Math.Clamp(v, 0, 1) * 65536)} \"{f}\"", null),

        // aplay (ALSA): one-shot only, no volume control.
        new("aplay", false,
            (f, _) => $"-q \"{f}\"", null),
    };

    private static string Pct(double v) =>
        ((int)Math.Round(Math.Clamp(v, 0, 1) * 100)).ToString(CultureInfo.InvariantCulture);

    /// <summary>True when <paramref name="cmd"/> resolves on PATH.</summary>
    private static bool Exists(string cmd)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("/usr/bin/env", $"which {cmd}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            if (p is null) return false;
            p.WaitForExit(1500);
            return p.HasExited && p.ExitCode == 0;
        }
        catch { return false; }
    }
}
