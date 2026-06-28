using System;
using System.IO;

namespace BardsTale.UI.Audio;

/// <summary>
/// Windows desktop music: loops each synthesized track through the Multimedia (MCI) API's
/// native <c>play … repeat</c> — a single, seamless loop with no process re-spawning and no
/// gap. Volume can be adjusted live, so crossfades between scenes work.
/// </summary>
public sealed class WindowsMusicService : IMusicService
{
    private const string Alias = "btmusic";
    private readonly object _gate = new();
    private GameMusic? _current;
    private double _volume = 0.45;

    public WindowsMusicService()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Stop();
    }

    public void Play(GameMusic track)
    {
        if (!OperatingSystem.IsWindows()) return;
        lock (_gate)
        {
            if (_current == track) return;
            StopLocked();
            try
            {
                var path = FileFor(track);
                if (!WinMm.Open(path, Alias)) return;
                WinMm.SetVolume(Alias, _volume);
                WinMm.Play(Alias, loop: true);
                _current = track;
            }
            catch { /* audio is non-essential */ }
        }
    }

    public void Stop()
    {
        lock (_gate) StopLocked();
    }

    public void SetVolume(double volume)
    {
        _volume = Math.Clamp(volume, 0, 1);
        lock (_gate)
            if (_current is not null) WinMm.SetVolume(Alias, _volume);
    }

    public bool SupportsLiveVolume => true; // MCI 'setaudio volume' takes effect on the playing track

    private void StopLocked()
    {
        if (_current is not null) WinMm.Close(Alias);
        _current = null;
    }

    private static string FileFor(GameMusic track)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bardstale_music_{track}.wav");
        if (!File.Exists(path))
            File.WriteAllBytes(path, MusicSynth.BuildWav(track));
        return path;
    }
}
