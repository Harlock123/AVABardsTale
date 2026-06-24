using System.Collections.Generic;
using System.IO;
using AVFoundation;
using Foundation;
using BardsTale.UI.Audio;
using BardsTale.UI.Settings;

namespace BardsTale.iOS;

/// <summary>
/// iOS audio backend: writes each synthesized sound to a temp WAV once and plays it via
/// <c>AVAudioPlayer</c>. A reference is held until playback finishes so it isn't collected.
/// </summary>
public sealed class IosAudioService : IAudioService
{
    private readonly List<AVAudioPlayer> _active = new();
    private readonly Dictionary<GameSound, NSUrl> _urls = new();
    private bool _sessionReady;

    public void Play(GameSound sound)
    {
        if (AppSettings.Current.Muted) return;
        var volume = (float)AppSettings.Current.SoundVolume;
        if (volume <= 0) return;

        try
        {
            EnsureSession();
            var player = AVAudioPlayer.FromUrl(UrlFor(sound));
            if (player is null) return;

            player.Volume = volume;
            _active.Add(player);
            // Do NOT Dispose() inside the player's own FinishedPlaying callback — AVFoundation
            // forbids it (it corrupts state and throws). Just drop the reference; GC reclaims it.
            player.FinishedPlaying += (_, _) => _active.Remove(player);
            player.PrepareToPlay();
            player.Play();
        }
        catch
        {
            // Audio is non-essential; never let playback disrupt the game.
        }
    }

    private void EnsureSession()
    {
        if (_sessionReady) return;
        var session = AVAudioSession.SharedInstance();
        session.SetCategory(AVAudioSessionCategory.Ambient);
        session.SetActive(true);
        _sessionReady = true;
    }

    private NSUrl UrlFor(GameSound sound)
    {
        if (_urls.TryGetValue(sound, out var url)) return url;
        var path = Path.Combine(Path.GetTempPath(), $"bardstale_{sound}.wav");
        File.WriteAllBytes(path, ToneSynth.BuildWav(sound));
        var made = NSUrl.FromFilename(path);
        _urls[sound] = made;
        return made;
    }
}
