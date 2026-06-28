using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BardsTale.UI.Audio;

/// <summary>
/// Linux desktop music: loops each synthesized track on a detected command-line player. Players
/// that can loop natively (ffplay, mpv) run a single seamless process; the rest (paplay, aplay)
/// are re-spawned per loop, re-armed a hair early to bridge launch jitter without a gap — the
/// same approach the macOS afplay backend uses. Silent if no player is installed. Volume changes
/// apply on the next loop (or the next track), so this reports no live-volume support.
/// </summary>
public sealed class LinuxMusicService : IMusicService
{
    private readonly object _gate = new();
    private readonly List<Process> _procs = new();
    private CancellationTokenSource? _cts;
    private GameMusic? _current;
    private double _volume = 0.45;

    private const int OverlapMs = 45;

    public LinuxMusicService()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Stop();
        Console.CancelKeyPress += (_, _) => Stop();
    }

    public void Play(GameMusic track)
    {
        if (!OperatingSystem.IsLinux()) return;
        if (LinuxPlayer.Detect() is null) return;
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
        var player = LinuxPlayer.Detect();
        if (player is null) return;

        // A player that loops natively just runs once and repeats itself — seamless, no re-spawn.
        if (player is { CanLoop: true, LoopArgs: { } loopArgs })
        {
            Spawn(player.Command, loopArgs(path, _volume), ct);
            return;
        }

        // Otherwise re-spawn each loop, re-arming a touch before it ends to cover launch jitter.
        var overlapMs = (int)Math.Min(OverlapMs, durationMs / 8);
        var waitMs = (int)Math.Max(1, durationMs - overlapMs);
        while (!ct.IsCancellationRequested)
        {
            if (!Spawn(player.Command, player.OneShotArgs(path, _volume), ct)) return;
            if (ct.WaitHandle.WaitOne(waitMs)) return;
        }
    }

    private bool Spawn(string command, string args, CancellationToken ct)
    {
        try
        {
            var proc = Process.Start(new ProcessStartInfo(command, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (proc is null) return false;

            lock (_gate)
            {
                if (ct.IsCancellationRequested) { try { proc.Kill(); } catch { } return false; }
                _procs.RemoveAll(HasExited);
                _procs.Add(proc);
            }
            return true;
        }
        catch
        {
            return false; // player missing or failed — give up quietly
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
