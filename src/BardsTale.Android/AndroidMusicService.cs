using System;
using Android.Media;
using BardsTale.UI.Audio;

namespace BardsTale.Android;

/// <summary>
/// Android music backend: plays each synthesized track loop through a static
/// <c>AudioTrack</c> with its loop points set to repeat forever. Switching tracks
/// stops the old one and starts a new one.
/// </summary>
public sealed class AndroidMusicService : IMusicService
{
    private readonly object _gate = new();
    private AudioTrack? _track;
    private GameMusic? _current;
    private float _volume = 0.45f;

    public void Play(GameMusic track)
    {
        lock (_gate)
        {
            if (_current == track && _track is not null) return;
            StopLocked();
            try
            {
                var pcm = MusicSynth.BuildPcm(track);
                var bytes = new byte[pcm.Length * 2];
                Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);

                var attributes = new AudioAttributes.Builder()
                    .SetUsage(AudioUsageKind.Media)!
                    .SetContentType(AudioContentType.Music)!
                    .Build()!;
                var format = new AudioFormat.Builder()
                    .SetEncoding(Encoding.Pcm16bit)!
                    .SetSampleRate(MusicSynth.SampleRate)!
                    .SetChannelMask(ChannelOut.Mono)!
                    .Build()!;
                var t = new AudioTrack.Builder()
                    .SetAudioAttributes(attributes)!
                    .SetAudioFormat(format)!
                    .SetBufferSizeInBytes(bytes.Length)!
                    .SetTransferMode(AudioTrackMode.Static)!
                    .Build();

                t.Write(bytes, 0, bytes.Length);
                t.SetLoopPoints(0, pcm.Length, -1); // mono → 1 frame per sample; -1 = loop forever
                t.SetVolume(_volume);
                t.Play();

                _track = t;
                _current = track;
            }
            catch
            {
                // Music is non-essential; never let playback disrupt the game.
            }
        }
    }

    public void Stop()
    {
        lock (_gate) StopLocked();
    }

    public void SetVolume(double volume)
    {
        _volume = (float)volume;
        lock (_gate)
        {
            try { _track?.SetVolume(_volume); } catch { /* gone */ }
        }
    }

    public bool SupportsLiveVolume => true; // AudioTrack.SetVolume applies instantly

    private void StopLocked()
    {
        _current = null;
        try { _track?.Stop(); _track?.Release(); _track?.Dispose(); } catch { /* already gone */ }
        _track = null;
    }
}
