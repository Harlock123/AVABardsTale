namespace BardsTale.UI.Audio;

/// <summary>The set of synthesized sound effects the game can play.</summary>
public enum GameSound
{
    UiConfirm,
    Door,
    Attack,
    SpellCast,
    Hurt,
    EnemyDefeated,
    Coin,
    LevelUp,
    Victory,
    Defeat,
    // Exploration
    FootstepStone,
    FootstepDungeon,
    StairsDown,
    StairsUp,
    // Town services
    Heal,
    Buy,
    Sell,
    Equip,
    // Spell flavours
    SpellFire,
    SpellBuff
}

/// <summary>Plays short sound effects. Implementations are per-platform; volume/mute
/// come from <see cref="Settings.AppSettings"/>.</summary>
public interface IAudioService
{
    void Play(GameSound sound);
}

/// <summary>Silent fallback used on platforms without an audio backend (and in tests).</summary>
public sealed class NullAudioService : IAudioService
{
    public void Play(GameSound sound) { }
}

/// <summary>Global access point. The active service is set once at startup by the app head.</summary>
public static class Sfx
{
    public static IAudioService Current { get; set; } = new NullAudioService();

    public static void Play(GameSound sound) => Current.Play(sound);
}
