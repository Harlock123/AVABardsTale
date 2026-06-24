using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using BardsTale.UI.Settings;

namespace BardsTale.UI.Audio;

/// <summary>
/// macOS desktop audio: writes each synthesized sound to a temp WAV once and plays it
/// with the built-in <c>afplay</c> tool. A no-op on non-macOS desktops for now (a native
/// backend for Windows/Linux is a follow-up).
/// </summary>
public sealed class DesktopAudioService : IAudioService
{
    private readonly Dictionary<GameSound, string> _files = new();

    public void Play(GameSound sound)
    {
        if (!OperatingSystem.IsMacOS()) return;
        if (AppSettings.Current.Muted) return;
        var volume = AppSettings.Current.SoundVolume;
        if (volume <= 0) return;

        try
        {
            var path = FileFor(sound);
            Process.Start(new ProcessStartInfo("afplay",
                $"-v {volume.ToString("0.00", CultureInfo.InvariantCulture)} \"{path}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch
        {
            // Audio is non-essential; never let a playback failure disrupt the game.
        }
    }

    private string FileFor(GameSound sound)
    {
        if (_files.TryGetValue(sound, out var existing) && File.Exists(existing)) return existing;
        var path = Path.Combine(Path.GetTempPath(), $"bardstale_{sound}.wav");
        File.WriteAllBytes(path, ToneSynth.BuildWav(sound));
        _files[sound] = path;
        return path;
    }
}
