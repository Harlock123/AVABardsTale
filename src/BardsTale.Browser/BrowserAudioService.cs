using System;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using BardsTale.UI.Audio;
using BardsTale.UI.Settings;

namespace BardsTale.Browser;

/// <summary>
/// Web Audio backend: plays the C#-synthesized WAV bytes through the browser's audio
/// engine (via audio.js). Decoded buffers are cached on the JS side by sound key.
/// </summary>
public sealed class BrowserAudioService : IAudioService
{
    public void Play(GameSound sound)
    {
        if (AppSettings.Current.Muted) return;
        var volume = AppSettings.Current.SoundVolume;
        if (volume <= 0) return;

        var wav = Convert.ToBase64String(ToneSynth.BuildWav(sound));
        _ = AudioInterop.Play(sound.ToString(), wav, volume);
    }
}

/// <summary>Binding to wwwroot/audio.js, imported once at startup (see Program.cs).</summary>
internal static partial class AudioInterop
{
    [JSImport("play", "audio")]
    public static partial Task Play(string key, string wavBase64, double volume);
}
