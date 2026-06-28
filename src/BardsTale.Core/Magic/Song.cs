namespace BardsTale.Core.Magic;

public enum SongEffect
{
    BuffPartyArmor,
    BuffPartyAttack,
    HealParty,
    Light,
    /// <summary>While sustained, mends a little of the whole party's wounds every round.</summary>
    RegenParty,
    /// <summary>While sustained, restores a little of the party's spell points every round.</summary>
    RestoreParty,
    /// <summary>While sustained, harms every enemy a little every round — a battle dirge.</summary>
    HarmEnemies
}

/// <summary>
/// A Bard's tune. Unlike spells, songs cost no spell points — but a Bard must keep
/// <em>playing</em> to sustain a song's effect (it lasts only the round it is sung), and
/// striking up a new tune spends one of the Bard's limited daily "tunes" until they rest.
/// </summary>
public sealed record Song(
    string Id,
    string Name,
    int Level,
    SongEffect Effect,
    int Power,
    string Description)
{
    public string Summary => Effect switch
    {
        SongEffect.BuffPartyArmor => $"sustained · party AC +{Power}",
        SongEffect.BuffPartyAttack => $"sustained · party hits +{Power}",
        SongEffect.HealParty => $"heal party ~{Power}",
        SongEffect.RegenParty => $"sustained · party regen {Power}/round",
        SongEffect.RestoreParty => $"sustained · party +{Power} SP/round",
        SongEffect.HarmEnemies => $"sustained · {Power} dmg to all foes/round",
        SongEffect.Light => "light",
        _ => ""
    };
}

public static class Songs
{
    public static readonly IReadOnlyList<Song> All = new[]
    {
        new Song("FALK", "Falkentyne's Fury", 1, SongEffect.BuffPartyAttack, 2,
            "A war-chant that drives the party to strike harder."),
        new Song("SANC", "Sanctuary Score", 1, SongEffect.BuffPartyArmor, 2,
            "A warding melody that turns aside enemy blows."),
        new Song("WATC", "Watchwood Melody", 1, SongEffect.Light, 0,
            "A bright air that lights the way through darkness."),
        new Song("LUCK", "Lucklaran's Lullaby", 3, SongEffect.HealParty, 4,
            "A soothing ballad that knits the party's wounds."),
        new Song("RENE", "Hymn of Renewal", 5, SongEffect.RegenParty, 3,
            "An enduring hymn that mends the party a little each round it is sustained."),
        new Song("MANA", "Cantata of Mana", 7, SongEffect.RestoreParty, 4,
            "A resonant cantata that feeds arcane vigour back to the party each round it is sung."),
        new Song("DIRG", "Dirge of the Doomed", 9, SongEffect.HarmEnemies, 6,
            "A dread dirge that withers every foe a little each round it is held."),
    };

    private static readonly Dictionary<string, Song> ById = All.ToDictionary(s => s.Id);

    public static Song Get(string id) => ById[id];

    public static IEnumerable<Song> KnownAtLevel(int level) => All.Where(s => s.Level <= level);

    public static IEnumerable<Song> LearnedAtLevel(int level) => All.Where(s => s.Level == level);
}
