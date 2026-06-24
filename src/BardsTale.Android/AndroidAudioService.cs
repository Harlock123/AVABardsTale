using System;
using System.Threading.Tasks;
using Android.Media;
using BardsTale.UI.Audio;
using BardsTale.UI.Settings;

namespace BardsTale.Android;

/// <summary>
/// Android audio backend: plays the synthesized 16-bit PCM through an <c>AudioTrack</c>
/// in static mode. A fresh track per sound lets effects overlap; each is released a
/// moment after it finishes.
/// </summary>
public sealed class AndroidAudioService : IAudioService
{
    public void Play(GameSound sound)
    {
        if (AppSettings.Current.Muted) return;
        var volume = (float)AppSettings.Current.SoundVolume;
        if (volume <= 0) return;

        try
        {
            var pcm = ToneSynth.BuildPcm(sound);
            var bytes = new byte[pcm.Length * 2];
            Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);

            var attributes = new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Game)!
                .SetContentType(AudioContentType.Sonification)!
                .Build()!;
            var format = new AudioFormat.Builder()
                .SetEncoding(Encoding.Pcm16bit)!
                .SetSampleRate(ToneSynth.SampleRate)!
                .SetChannelMask(ChannelOut.Mono)!
                .Build()!;
            var track = new AudioTrack.Builder()
                .SetAudioAttributes(attributes)!
                .SetAudioFormat(format)!
                .SetBufferSizeInBytes(bytes.Length)!
                .SetTransferMode(AudioTrackMode.Static)!
                .Build();

            track.Write(bytes, 0, bytes.Length);
            track.SetVolume(volume);
            track.Play();

            var durationMs = pcm.Length * 1000 / ToneSynth.SampleRate + 150;
            _ = Task.Delay(durationMs).ContinueWith(_ =>
            {
                try { track.Stop(); track.Release(); track.Dispose(); }
                catch { /* already gone */ }
            });
        }
        catch
        {
            // Audio is non-essential; never let playback disrupt the game.
        }
    }
}
