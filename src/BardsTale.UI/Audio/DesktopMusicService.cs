using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BardsTale.UI.Audio;

/// <summary>
/// macOS desktop music: writes each track's loop to a temp WAV once and plays it on a
/// repeat with <c>afplay</c> (which has no native loop), restarting the moment it ends.
/// A no-op on non-macOS desktops for now. Volume changes apply on the next loop.
/// </summary>
public sealed class DesktopMusicService : IMusicService
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Process? _proc;
    private GameMusic? _current;
    private double _volume = 0.45;

    public void Play(GameMusic track)
    {
        if (!OperatingSystem.IsMacOS()) return;
        lock (_gate)
        {
            if (_current == track && _cts is { IsCancellationRequested: false }) return;
            StopLocked();
            _current = track;
            var path = FileFor(track);
            var cts = new CancellationTokenSource();
            _cts = cts;
            Task.Run(() => Loop(path, cts.Token));
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
        try { _proc?.Kill(); } catch { /* already gone */ }
        _proc = null;
    }

    private void Loop(string path, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var vol = _volume.ToString("0.00", CultureInfo.InvariantCulture);
                var proc = Process.Start(new ProcessStartInfo("afplay", $"-v {vol} \"{path}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (proc is null) return;
                lock (_gate) { if (ct.IsCancellationRequested) { try { proc.Kill(); } catch { } return; } _proc = proc; }
                proc.WaitForExit();
            }
            catch
            {
                return; // afplay missing or failed — give up quietly
            }
        }
    }

    private static string FileFor(GameMusic track)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bardstale_music_{track}.wav");
        if (!File.Exists(path))
            File.WriteAllBytes(path, MusicSynth.BuildWav(track));
        return path;
    }
}
