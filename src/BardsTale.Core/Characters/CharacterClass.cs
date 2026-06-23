namespace BardsTale.Core.Characters;

public enum CharacterClass
{
    Warrior,
    Paladin,
    Rogue,
    Bard,
    Hunter,
    Monk,
    Conjurer,
    Magician,
    Sorcerer,
    Wizard
}

/// <summary>The schools of magic available to spellcasting classes.</summary>
public enum MagicSchool
{
    None,
    Conjurer,
    Magician,
    Sorcerer,
    Wizard
}

/// <summary>
/// Static definition of a class: how it fights, how much health it gains,
/// and whether it casts spells.
/// </summary>
public sealed record ClassDefinition(
    CharacterClass Class,
    string Name,
    MagicSchool School,
    int HitDieSides,
    int BaseHitBonus,
    int AttacksPerLevelDivisor,
    bool CanWearHeavyArmor)
{
    public bool IsSpellcaster => School != MagicSchool.None;
}

public static class Classes
{
    public static readonly IReadOnlyDictionary<CharacterClass, ClassDefinition> All = new[]
    {
        //                  class                       name          school                  hitDie hitBonus atkDiv heavy
        new ClassDefinition(CharacterClass.Warrior,  "Warrior",  MagicSchool.None,        12, 2, 10, true),
        new ClassDefinition(CharacterClass.Paladin,  "Paladin",  MagicSchool.None,        12, 2, 12, true),
        new ClassDefinition(CharacterClass.Rogue,    "Rogue",    MagicSchool.None,         6, 0, 14, false),
        new ClassDefinition(CharacterClass.Bard,     "Bard",     MagicSchool.None,         8, 1, 14, true),
        new ClassDefinition(CharacterClass.Hunter,   "Hunter",   MagicSchool.None,         8, 1, 13, true),
        new ClassDefinition(CharacterClass.Monk,     "Monk",     MagicSchool.None,         8, 1,  8, false),
        new ClassDefinition(CharacterClass.Conjurer, "Conjurer", MagicSchool.Conjurer,     4, 0, 20, false),
        new ClassDefinition(CharacterClass.Magician, "Magician", MagicSchool.Magician,     4, 0, 20, false),
        new ClassDefinition(CharacterClass.Sorcerer, "Sorcerer", MagicSchool.Sorcerer,     4, 0, 20, false),
        new ClassDefinition(CharacterClass.Wizard,   "Wizard",   MagicSchool.Wizard,       4, 0, 20, false),
    }.ToDictionary(c => c.Class);

    public static ClassDefinition Get(CharacterClass c) => All[c];
}
