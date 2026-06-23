using BardsTale.Core.Characters;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Util;

namespace BardsTale.Core.Game;

/// <summary>Convenience factory that assembles a ready-to-play game for the vertical slice.</summary>
public static class NewGame
{
    private static readonly (string Name, Race Race, CharacterClass Class)[] DefaultRoster =
    {
        ("Brÿnn",   Race.Dwarf,   CharacterClass.Warrior),
        ("Aldous",  Race.Human,   CharacterClass.Paladin),
        ("Sable",   Race.Hobbit,  CharacterClass.Rogue),
        ("Lyric",   Race.HalfElf, CharacterClass.Bard),
        ("Mirelle", Race.Elf,     CharacterClass.Conjurer),
        ("Vex",     Race.Gnome,   CharacterClass.Magician),
    };

    public static GameState CreateDefault(int? seed = null)
    {
        var rng = new SystemRandomSource(seed);
        var party = CreateDefaultParty(rng);
        var maze = new MazeBuilder(rng).Build("Catacombs — Level 1", 16, 16);
        return new GameState(party, maze, rng);
    }

    public static Party CreateDefaultParty(IRandomSource rng)
    {
        var factory = new CharacterFactory(rng);
        var party = new Party();
        foreach (var (name, race, cls) in DefaultRoster)
            party.Add(factory.Create(name, race, cls));
        return party;
    }
}
