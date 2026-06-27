using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BardsTale.UI.Audio;

/// <summary>
/// macOS desktop music: writes each track's loop to a temp WAV once and plays it on a
/// repeat with <c>afplay</c> (which has no native loop). Because a fresh <c>afplay</c>
/// takes ~100&#160;ms to begin producing sound, naively waiting for one to finish before
/// starting the next leaves an audible gap at every loop point. Instead we re-arm the
/// next <c>afplay</c> slightly <em>before</em> the current one ends, so the two overlap
/// just long enough to hide that start-up latency — making the loop sound seamless.
/// A no-op on non-macOS desktops for now. Volume changes apply on the next loop.
/// </summary>
public sealed class DesktopMusicService : IMusicService
{
    private readonly object _gate = new();
    private readonly List<Process> _procs = new();
    private CancellationTokenSource? _cts;
    private GameMusic? _current;
    private double _volume = 0.45;

    public DesktopMusicService()
    {
        // afplay runs as a child process; on Unix it would otherwise be orphaned (and keep
        // playing) when the game exits. Stop it on process exit and Ctrl-C so nothing lingers.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Stop();
        Console.CancelKeyPress += (_, _) => Stop();
    }

    public void Play(GameMusic track)
    {
        if (!OperatingSystem.IsMacOS()) return;
        lock (_gate)
        {
            if (_current == track && _cts is { IsCancellationRequested: false }) return;
            StopLocked();
            _current = track;
            var path = FileFor(track);
            var durationMs = MusicSynth.BuildPcm(track).Length * 1000L / MusicSynth.SampleRate;
            var cts = new CancellationTokenSource();
            _cts = cts;
            Task.Run(() => Loop(path, durationMs, cts.Token));
        }
    }

    public void Stop()
    {
        lock (_gate) StopLocked();
    }

    public void SetVolume(double volume) => _volume = Math.Clamp(volume, 0, 1);

    private void StopLocked()
    {
        _current = null;
        _cts?.Cancel();
        _cts = null;
        foreach (var p in _procs)
            try { p.Kill(); } catch { /* already gone */ }
        _procs.Clear();
    }

    private void Loop(string path, long durationMs, CancellationToken ct)
    {
        // Start the next play this far before the current one ends, so its start-up
        // latency is covered and playback never falls silent. Clamp for short clips.
        var overlapMs = (int)Math.Min(140, durationMs / 4);
        var waitMs = (int)Math.Max(1, durationMs - overlapMs);

        while (!ct.IsCancellationRequested)
        {
            if (!StartAfplay(path, ct)) return;
            // Sleep until just before this play ends — waking early if music is stopped.
            if (ct.WaitHandle.WaitOne(waitMs)) return;
        }
    }

    private bool StartAfplay(string path, CancellationToken ct)
    {
        try
        {
            var vol = _volume.ToString("0.00", CultureInfo.InvariantCulture);
            var proc = Process.Start(new ProcessStartInfo("afplay", $"-v {vol} \"{path}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (proc is null) return false;

            lock (_gate)
            {
                if (ct.IsCancellationRequested) { try { proc.Kill(); } catch { } return false; }
                _procs.RemoveAll(HasExited);   // drop the just-finished previous loop
                _procs.Add(proc);
            }
            return true;
        }
        catch
        {
            return false; // afplay missing or failed — give up quietly
        }
    }

    private static bool HasExited(Process p)
    {
        try { return p.HasExited; } catch { return true; }
    }

    private static string FileFor(GameMusic track)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bardstale_music_{track}.wav");
        if (!File.Exists(path))
            File.WriteAllBytes(path, MusicSynth.BuildWav(track));
        return path;
    }
}
