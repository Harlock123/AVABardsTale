namespace BardsTale.Core.Characters;

public enum Race
{
    Human,
    Elf,
    Dwarf,
    Hobbit,
    HalfElf,
    HalfOrc,
    Gnome
}

/// <summary>Per-race attribute modifiers applied at character creation.</summary>
public sealed record RaceDefinition(
    Race Race,
    string Name,
    int Strength,
    int Intelligence,
    int Dexterity,
    int Constitution,
    int Luck)
{
    public void ApplyTo(AttributeSet attrs)
    {
        attrs.Strength += Strength;
        attrs.Intelligence += Intelligence;
        attrs.Dexterity += Dexterity;
        attrs.Constitution += Constitution;
        attrs.Luck += Luck;
    }
}

public static class Races
{
    public static readonly IReadOnlyDictionary<Race, RaceDefinition> All = new[]
    {
        new RaceDefinition(Race.Human,   "Human",     0,  0,  0,  0,  0),
        new RaceDefinition(Race.Elf,     "Elf",      -1,  1,  1,  0,  1),
        new RaceDefinition(Race.Dwarf,   "Dwarf",     1,  0, -1,  2,  0),
        new RaceDefinition(Race.Hobbit,  "Hobbit",   -2,  0,  2,  0,  2),
        new RaceDefinition(Race.HalfElf, "Half-Elf",  0,  1,  1,  0,  0),
        new RaceDefinition(Race.HalfOrc, "Half-Orc",  2,  0,  0,  1, -1),
        new RaceDefinition(Race.Gnome,   "Gnome",    -1,  2,  0,  0,  1),
    }.ToDictionary(r => r.Race);

    public static RaceDefinition Get(Race race) => All[race];
}
