using System;
using System.Collections.Generic;
using System.IO;
using BardsTale.UI.Settings;

namespace BardsTale.UI.Audio;

/// <summary>
/// Windows desktop sound effects: plays each synthesized sound through the Multimedia (MCI)
/// API under its own alias, so several effects can overlap and none interrupts the music
/// (which uses a separate alias). Finished aliases are closed lazily on the next play, with a
/// hard cap so concurrent voices can't grow without bound. No audio NuGet dependency.
/// </summary>
public sealed class WindowsAudioService : IAudioService
{
    private const int MaxConcurrent = 8;
    private readonly object _gate = new();
    // MCI can't set a playing waveaudio stream's volume, so the level is baked into the WAV;
    // files are cached per (sound, volume%) so a steady volume re-renders each effect only once.
    private readonly Dictionary<(GameSound Sound, int VolumePct), string> _files = new();
    private readonly List<string> _active = new(); // live MCI aliases, oldest first
    private int _counter;

    public WindowsAudioService()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => CloseAll();
    }

    public void Play(GameSound sound)
    {
        if (!OperatingSystem.IsWindows()) return;
        if (AppSettings.Current.Muted) return;
        var volume = AppSettings.Current.SoundVolume;
        if (volume <= 0) return;

        lock (_gate)
        {
            try
            {
                Reap();
                if (_active.Count >= MaxConcurrent) CloseAt(0); // drop the oldest to make room

                var path = FileFor(sound, volume); // volume baked into the WAV (MCI can't set it live)
                var alias = $"btsfx{_counter++}";
                if (!WinMm.Open(path, alias)) return;
                WinMm.Play(alias);
                _active.Add(alias);
            }
            catch { /* audio is non-essential */ }
        }
    }

    // Close any aliases whose sound has finished.
    private void Reap()
    {
        for (var i = _active.Count - 1; i >= 0; i--)
            if (WinMm.IsStopped(_active[i]))
            {
                WinMm.Close(_active[i]);
                _active.RemoveAt(i);
            }
    }

    private void CloseAt(int index)
    {
        WinMm.Close(_active[index]);
        _active.RemoveAt(index);
    }

    private void CloseAll()
    {
        lock (_gate)
        {
            foreach (var a in _active) WinMm.Close(a);
            _active.Clear();
        }
    }

    private string FileFor(GameSound sound, double volume)
    {
        var pct = (int)Math.Round(Math.Clamp(volume, 0, 1) * 100);
        var key = (sound, pct);
        if (_files.TryGetValue(key, out var existing) && File.Exists(existing)) return existing;
        var path = Path.Combine(Path.GetTempPath(), $"bardstale_{sound}_v{pct}.wav");
        File.WriteAllBytes(path, ToneSynth.BuildWav(sound, pct / 100.0));
        _files[key] = path;
        return path;
    }
}
