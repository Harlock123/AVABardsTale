using BardsTale.Core.Characters;

namespace BardsTale.Core.Combat;

public enum MonsterSpellKind
{
    /// <summary>Lulls one or more party members to sleep.</summary>
    SleepFoe,
    /// <summary>Mends the most-wounded living ally in the encounter.</summary>
    HealAllies,
    /// <summary>Blasts the whole party with area damage (a luck save halves it).</summary>
    BlastParty,
    /// <summary>Hurls a focused bolt at a single party member.</summary>
    DamageFoe,
    /// <summary>Calls reinforcements into the fight.</summary>
    Summon
}

/// <summary>
/// A spell a monster may cast on its turn instead of attacking. For <see cref="MonsterSpellKind.Summon"/>,
/// <paramref name="SummonTemplate"/> is what it calls and <paramref name="Power"/> is how many.
/// </summary>
public sealed record MonsterSpell(string Name, MonsterSpellKind Kind, int Power, double Chance,
    MonsterTemplate? SummonTemplate = null);

/// <summary>A nasty rider some monsters apply on a successful hit.</summary>
public enum MonsterAbility
{
    None,
    /// <summary>Saps a level from the victim, lowering their max HP/SP.</summary>
    DrainLevel,
    /// <summary>Saps a point from one of the victim's attributes.</summary>
    DrainStat,
    /// <summary>Snatches gold from the party purse.</summary>
    StealGold
}

/// <summary>Static stat block for a kind of monster.</summary>
public sealed record MonsterTemplate(
    string Name,
    int MaxHitPoints,
    int ArmorClass,
    int AttackDice,
    int AttackSides,
    int AttackBonus,
    int ExperienceValue,
    int GoldValue,
    int MaxPerGroup,
    int Speed = 2,
    StatusEffect InflictsStatus = StatusEffect.None,
    double StatusChance = 0.0,
    MonsterSpell? Spell = null,
    MonsterAbility Ability = MonsterAbility.None,
    double AbilityChance = 0.0)
{
    public bool IsCaster => Spell is not null;

    /// <summary>A short verb describing the status this monster can inflict, for combat narration.</summary>
    public string StatusVerb => InflictsStatus switch
    {
        StatusEffect.Poisoned => "poisons",
        StatusEffect.Paralyzed => "paralyzes",
        StatusEffect.Asleep => "lulls",
        _ => ""
    };
}

/// <summary>A live monster instance in combat.</summary>
public sealed class Monster
{
    public required MonsterTemplate Template { get; init; }
    public int HitPoints { get; set; }

    public string Name => Template.Name;
    public int ArmorClass => Template.ArmorClass;
    public bool IsDead => HitPoints <= 0;
    public bool IsWounded => !IsDead && HitPoints < Template.MaxHitPoints;

    public void Heal(int amount)
    {
        if (IsDead) return;
        HitPoints = Math.Min(Template.MaxHitPoints, HitPoints + amount);
    }

    public static Monster Spawn(MonsterTemplate t) => new()
    {
        Template = t,
        HitPoints = t.MaxHitPoints
    };
}

public static class Bestiary
{
    public static readonly MonsterTemplate Skeleton =
        new("Skeleton", 8, 8, 1, 6, 0, 40, 8, 4);

    public static readonly MonsterTemplate GiantRat =
        new("Giant Rat", 4, 9, 1, 4, 0, 15, 2, 6, Speed: 3);

    public static readonly MonsterTemplate Kobold =
        new("Kobold", 5, 8, 1, 6, 0, 20, 5, 5);

    public static readonly MonsterTemplate GiantSpider =
        new("Giant Spider", 7, 7, 1, 4, 0, 35, 6, 4, Speed: 4,
            InflictsStatus: StatusEffect.Poisoned, StatusChance: 0.40);

    public static readonly MonsterTemplate Berserker =
        new("Berserker", 14, 7, 1, 8, 1, 70, 20, 3);

    public static readonly MonsterTemplate Ghoul =
        new("Ghoul", 16, 6, 1, 6, 1, 90, 18, 3, Speed: 2,
            InflictsStatus: StatusEffect.Paralyzed, StatusChance: 0.25);

    public static readonly MonsterTemplate WillOWisp =
        new("Will-o-Wisp", 12, 4, 1, 4, 0, 80, 0, 2, Speed: 5,
            InflictsStatus: StatusEffect.Asleep, StatusChance: 0.35);

    public static readonly MonsterTemplate Wraith =
        new("Wraith", 20, 5, 2, 6, 2, 150, 30, 2, Speed: 2,
            Ability: MonsterAbility.DrainLevel, AbilityChance: 0.30);

    public static readonly MonsterTemplate Cutpurse =
        new("Cutpurse", 8, 8, 1, 4, 0, 35, 0, 4, Speed: 5,
            Ability: MonsterAbility.StealGold, AbilityChance: 0.50);

    public static readonly MonsterTemplate CryptCrawler =
        new("Crypt Crawler", 18, 7, 1, 6, 0, 100, 15, 2, Speed: 2,
            Ability: MonsterAbility.DrainStat, AbilityChance: 0.30);

    public static readonly MonsterTemplate CovenWitch =
        new("Coven Witch", 10, 7, 1, 4, 0, 75, 25, 2, Speed: 3,
            Spell: new MonsterSpell("Slumber", MonsterSpellKind.SleepFoe, Power: 2, Chance: 0.45));

    public static readonly MonsterTemplate MadGodAcolyte =
        new("Mad God Acolyte", 14, 6, 1, 6, 0, 95, 30, 2, Speed: 2,
            Spell: new MonsterSpell("Mending Chant", MonsterSpellKind.HealAllies, Power: 10, Chance: 0.55));

    public static readonly MonsterTemplate SparkImp =
        new("Spark Imp", 6, 7, 1, 3, 0, 50, 12, 3, Speed: 4,
            Spell: new MonsterSpell("Spark", MonsterSpellKind.BlastParty, Power: 5, Chance: 0.40));

    public static readonly MonsterTemplate FlameShade =
        new("Flame Shade", 16, 5, 1, 6, 0, 130, 35, 2, Speed: 3,
            Spell: new MonsterSpell("Cinderblast", MonsterSpellKind.BlastParty, Power: 9, Chance: 0.50));

    public static readonly MonsterTemplate HexAdept =
        new("Hex Adept", 14, 6, 1, 4, 0, 120, 40, 2, Speed: 3,
            Spell: new MonsterSpell("Soul Bolt", MonsterSpellKind.DamageFoe, Power: 14, Chance: 0.55));

    public static readonly MonsterTemplate Necromancer =
        new("Necromancer", 16, 6, 1, 4, 0, 130, 30, 1, Speed: 2,
            Spell: new MonsterSpell("Summon Dead", MonsterSpellKind.Summon, Power: 2, Chance: 0.40, SummonTemplate: Skeleton));

    public static readonly IReadOnlyList<MonsterTemplate> Common =
        new[] { Skeleton, GiantRat, Kobold, GiantSpider, CovenWitch, SparkImp, Cutpurse };

    public static readonly IReadOnlyList<MonsterTemplate> Tough =
        new[] { Berserker, Ghoul, WillOWisp, Wraith, CovenWitch, MadGodAcolyte, FlameShade, HexAdept, Necromancer, CryptCrawler };
}
