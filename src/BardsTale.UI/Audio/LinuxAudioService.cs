using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using BardsTale.UI.Settings;

namespace BardsTale.UI.Audio;

/// <summary>
/// Linux desktop sound effects: writes each synthesized sound to a temp WAV once and plays it
/// on a detected command-line player (ffplay / mpv / paplay / aplay). Each effect spawns its
/// own short-lived process, so they can overlap. Silent if no player is installed.
/// </summary>
public sealed class LinuxAudioService : IAudioService
{
    private readonly Dictionary<GameSound, string> _files = new();

    public void Play(GameSound sound)
    {
        if (!OperatingSystem.IsLinux()) return;
        if (AppSettings.Current.Muted) return;
        var volume = AppSettings.Current.SoundVolume;
        if (volume <= 0) return;
        if (LinuxPlayer.Detect() is not { } player) return;

        try
        {
            var path = FileFor(sound);
            Process.Start(new ProcessStartInfo(player.Command, player.OneShotArgs(path, volume))
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
