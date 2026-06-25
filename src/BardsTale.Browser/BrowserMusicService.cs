using System;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using BardsTale.UI.Audio;

namespace BardsTale.Browser;

/// <summary>
/// Web Audio music backend: plays the C#-synthesized track WAV on a looping buffer
/// source (via music.js). Decoded tracks are cached on the JS side by key.
/// </summary>
public sealed class BrowserMusicService : IMusicService
{
    private double _volume = 0.45;

    public void Play(GameMusic track)
    {
        var wav = Convert.ToBase64String(MusicSynth.BuildWav(track));
        _ = MusicInterop.Play(track.ToString(), wav, _volume);
    }

    public void Stop() => MusicInterop.Stop();

    public void SetVolume(double volume)
    {
        _volume = volume;
        MusicInterop.SetVolume(volume);
    }
}

/// <summary>Binding to wwwroot/music.js, imported once at startup (see Program.cs).</summary>
internal static partial class MusicInterop
{
    [JSImport("playMusic", "music")]
    public static partial Task Play(string key, string wavBase64, double volume);

    [JSImport("stopMusic", "music")]
    public static partial void Stop();

    [JSImport("setMusicVolume", "music")]
    public static partial void SetVolume(double volume);
}
