using BardsTale.UI.Settings;

namespace BardsTale.UI.Audio;

/// <summary>The looping background tracks the game can play, chosen by game state.</summary>
public enum GameMusic
{
    /// <summary>Warm, folk-like theme for the town of Skara Brae.</summary>
    Town,
    /// <summary>Slow, ominous ambience for exploring the catacombs.</summary>
    Dungeon,
    /// <summary>Fast, driving theme during combat.</summary>
    Combat,
    /// <summary>Triumphant fanfare for the victory screen.</summary>
    Victory
}

/// <summary>
/// Plays looping background music. Implementations are per-platform; unlike the
/// one-shot <see cref="IAudioService"/>, a track plays continuously until changed or
/// stopped. Volume/enable come from <see cref="AppSettings"/>, applied via <see cref="Music"/>.
/// </summary>
public interface IMusicService
{
    /// <summary>Starts looping the given track (replacing any current one).</summary>
    void Play(GameMusic track);

    /// <summary>Stops all music.</summary>
    void Stop();

    /// <summary>Sets the playback volume (0–1), live where the platform supports it.</summary>
    void SetVolume(double volume);
}

/// <summary>Silent fallback for platforms without a music backend (and tests).</summary>
public sealed class NullMusicService : IMusicService
{
    public void Play(GameMusic track) { }
    public void Stop() { }
    public void SetVolume(double volume) { }
}

/// <summary>
/// Global music control. The game asks for a track via <see cref="Play"/>; this facade
/// honours the user's music settings (enabled / volume / global mute) and avoids
/// restarting a track that's already playing. Call <see cref="RefreshSettings"/> when
/// the relevant settings change.
/// </summary>
public static class Music
{
    private static readonly object Gate = new();

    /// <summary>The active backend, set once at startup by the app head.</summary>
    public static IMusicService Current { get; set; } = new NullMusicService();

    private static GameMusic? _desired; // what the game wants playing
    private static GameMusic? _playing; // what the backend is currently playing

    /// <summary>Requests a track. Idempotent — asking for the current track again does nothing.</summary>
    public static void Play(GameMusic track)
    {
        lock (Gate)
        {
            _desired = track;
            Apply();
        }
    }

    /// <summary>Stops the music entirely until the next <see cref="Play"/>.</summary>
    public static void Stop()
    {
        lock (Gate)
        {
            _desired = null;
            Apply();
        }
    }

    /// <summary>Re-evaluates music settings (enable / volume / mute) against the desired track.</summary>
    public static void RefreshSettings()
    {
        lock (Gate)
        {
            Apply();
        }
    }

    private static void Apply()
    {
        var s = AppSettings.Current;
        var on = _desired is not null && s.MusicEnabled && !s.Muted && s.MusicVolume > 0;
        if (on)
        {
            Current.SetVolume(s.MusicVolume);
            if (_playing != _desired)
            {
                Current.Play(_desired!.Value);
                _playing = _desired;
            }
        }
        else if (_playing is not null)
        {
            Current.Stop();
            _playing = null;
        }
    }
}
