namespace BardsTale.Core.Magic;

public enum SongEffect
{
    BuffPartyArmor,
    BuffPartyAttack,
    HealParty,
    Light
}

/// <summary>
/// A Bard's tune. Unlike spells, songs cost no spell points — a Bard simply plays
/// one each round, shaping the battle with party-wide effects.
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
        SongEffect.BuffPartyArmor => $"party AC +{Power}",
        SongEffect.BuffPartyAttack => $"party hits +{Power}",
        SongEffect.HealParty => $"heal party ~{Power}",
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
    };

    private static readonly Dictionary<string, Song> ById = All.ToDictionary(s => s.Id);

    public static Song Get(string id) => ById[id];

    public static IEnumerable<Song> KnownAtLevel(int level) => All.Where(s => s.Level <= level);

    public static IEnumerable<Song> LearnedAtLevel(int level) => All.Where(s => s.Level == level);
}
