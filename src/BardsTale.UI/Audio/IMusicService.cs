using System.Threading;
using System.Threading.Tasks;
using BardsTale.UI.Settings;

namespace BardsTale.UI.Audio;

/// <summary>The looping background tracks the game can play, chosen by game state.</summary>
public enum GameMusic
{
    /// <summary>Warm, folk-like theme for the town of Skara Brae.</summary>
    Town,
    /// <summary>Slow, ominous ambience for the upper catacombs (floors 1–6).</summary>
    Dungeon,
    /// <summary>Fast, driving theme during combat.</summary>
    Combat,
    /// <summary>Triumphant fanfare for the victory screen.</summary>
    Victory,
    /// <summary>A darker, brooding ambience for the mid catacombs (floors 7–13).</summary>
    DungeonDeep,
    /// <summary>A slow, dissonant theme of dread for the deepest floors (14+).</summary>
    DungeonAbyss
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

    /// <summary>
    /// True when <see cref="SetVolume"/> takes effect immediately on the playing track — the
    /// prerequisite for a volume crossfade. Backends that can only change volume on the next
    /// loop (e.g. desktop <c>afplay</c>) return false and get a clean cut instead.
    /// </summary>
    bool SupportsLiveVolume => false;
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
    private static GameMusic? _playing; // what the backend is (or will, mid-fade, be) playing

    private static CancellationTokenSource? _fadeCts; // in-flight crossfade, if any
    private const int FadeMs = 700;

    /// <summary>Requests a track. Idempotent — asking for the current track again does nothing.</summary>
    public static void Play(GameMusic track)
    {
        lock (Gate)
        {
            _desired = track;
            Apply();
        }
    }

    /// <summary>The catacomb ambience for a dungeon depth — it darkens as the party descends.</summary>
    public static GameMusic DungeonTheme(int depth) =>
        depth >= 14 ? GameMusic.DungeonAbyss :
        depth >= 7 ? GameMusic.DungeonDeep :
        GameMusic.Dungeon;

    /// <summary>Plays the depth-appropriate catacomb ambience (crossfading at the tier boundaries).</summary>
    public static void PlayDungeon(int depth) => Play(DungeonTheme(depth));

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

        if (!on)
        {
            CancelFade();
            if (_playing is not null) { Current.Stop(); _playing = null; }
            return;
        }

        if (_playing == _desired)
        {
            // Already on the right track: keep the live volume in sync, unless a fade owns it.
            if (_fadeCts is null) Current.SetVolume(s.MusicVolume);
            return;
        }

        // A scene change. Crossfade when the backend can ramp volume live and the user wants it;
        // otherwise switch immediately (e.g. desktop afplay, which can't fade a running clip).
        if (Current.SupportsLiveVolume && s.CrossfadeMusic)
        {
            StartFade(_desired!.Value, s.MusicVolume, fadeOutFirst: _playing is not null);
        }
        else
        {
            CancelFade();
            Current.Play(_desired!.Value);
            Current.SetVolume(s.MusicVolume);
            _playing = _desired;
        }
    }

    /// <summary>Kicks off a background crossfade to <paramref name="track"/>, cancelling any prior one.</summary>
    private static void StartFade(GameMusic track, double target, bool fadeOutFirst)
    {
        CancelFade();
        var cts = new CancellationTokenSource();
        _fadeCts = cts;
        _playing = track; // claim the target now, so a repeat request for it mid-fade is a no-op
        var backend = Current;
        var fader = new MusicCrossfader(backend, FadeMs);
        Task.Run(() =>
        {
            try { fader.Run(track, target, fadeOutFirst, cts.Token); }
            catch { /* music is non-essential */ }
            finally { lock (Gate) { if (_fadeCts == cts) _fadeCts = null; } }
        });
    }

    private static void CancelFade()
    {
        _fadeCts?.Cancel();
        _fadeCts = null;
    }
}
