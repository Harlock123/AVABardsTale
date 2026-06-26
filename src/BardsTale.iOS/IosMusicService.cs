using System.Collections.Generic;
using System.IO;
using AVFoundation;
using Foundation;
using BardsTale.UI.Audio;

namespace BardsTale.iOS;

/// <summary>
/// iOS music backend: plays each synthesized track loop through an <c>AVAudioPlayer</c>
/// set to repeat indefinitely (<c>NumberOfLoops = -1</c>). Switching tracks stops the
/// previous player and starts a new one.
/// </summary>
public sealed class IosMusicService : IMusicService
{
    private readonly Dictionary<GameMusic, NSUrl> _urls = new();
    private AVAudioPlayer? _player;
    private GameMusic? _current;
    private float _volume = 0.45f;
    private bool _sessionReady;

    public void Play(GameMusic track)
    {
        if (_current == track && _player is { Playing: true }) return;
        try
        {
            EnsureSession();
            Stop();
            var player = AVAudioPlayer.FromUrl(UrlFor(track));
            if (player is null) return;

            player.NumberOfLoops = -1; // loop forever
            player.Volume = _volume;
            player.PrepareToPlay();
            player.Play();
            _player = player;
            _current = track;
        }
        catch
        {
            // Music is non-essential.
        }
    }

    public void Stop()
    {
        _current = null;
        try { _player?.Stop(); _player?.Dispose(); } catch { /* already gone */ }
        _player = null;
    }

    public void SetVolume(double volume)
    {
        _volume = (float)volume;
        if (_player is not null) _player.Volume = _volume;
    }

    public bool SupportsLiveVolume => true; // AVAudioPlayer.Volume applies instantly

    private void EnsureSession()
    {
        if (_sessionReady) return;
        var session = AVAudioSession.SharedInstance();
        session.SetCategory(AVAudioSessionCategory.Ambient);
        session.SetActive(true);
        _sessionReady = true;
    }

    private NSUrl UrlFor(GameMusic track)
    {
        if (_urls.TryGetValue(track, out var url)) return url;
        var path = Path.Combine(Path.GetTempPath(), $"bardstale_music_{track}.wav");
        File.WriteAllBytes(path, MusicSynth.BuildWav(track));
        var made = NSUrl.FromFilename(path);
        _urls[track] = made;
        return made;
    }
}
