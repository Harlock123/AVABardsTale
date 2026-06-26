using System;
using System.Collections.Generic;
using System.Linq;
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
    MonsterTemplate? SummonTemplate = null)
{
    /// <summary>The damage element of this spell or breath, used against party-side resistances.</summary>
    public Element Element => MonsterElements.OfSpell(Name);
}

/// <summary>
/// Promotes a wandering monster to an "elite": a buffed, lone champion worth far more XP,
/// gold and loot. Elites keep the base creature's abilities and elemental affinities (the
/// "Elite " name prefix is stripped for those lookups), so an Elite Skeleton still burns.
/// </summary>
public static class Elites
{
    public static MonsterTemplate Promote(MonsterTemplate t) => t with
    {
        Name = $"Elite {t.Name}",
        MaxHitPoints = (int)Math.Round(t.MaxHitPoints * 1.8),
        AttackBonus = t.AttackBonus + 2,
        ArmorClass = Math.Max(0, t.ArmorClass - 1),
        ExperienceValue = (int)Math.Round(t.ExperienceValue * 2.5),
        GoldValue = (int)Math.Round(t.GoldValue * 2.5),
        Speed = t.Speed + 1,
        MaxPerGroup = 1,
        IsElite = true
    };

    /// <summary>The chance a wandering pack is led by an elite — rising slowly with depth.</summary>
    public static double ChanceForDepth(int depth) => Math.Min(0.25, 0.06 + 0.012 * depth);
}

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
    double AbilityChance = 0.0,
    bool IsElite = false)
{
    public bool IsCaster => Spell is not null;

    /// <summary>The underlying creature's name with any "Elite " prefix stripped (for codex/affinities).</summary>
    public string BaseName => IsElite && Name.StartsWith("Elite ", StringComparison.Ordinal)
        ? Name["Elite ".Length..]
        : Name;

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

    /// <summary>
    /// Set once when a boss or elite champion is driven below its enrage threshold: it
    /// then strikes harder, faster and casts more readily for the rest of the fight.
    /// </summary>
    public bool Enraged { get; set; }

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

/// <summary>
/// The monster roster: every wandering creature in ten toughness tiers, weakest to
/// strongest, spread across the twenty floors of the catacombs. The lair bosses live
/// in <see cref="Bosses"/>. Templates are referenced by name, so their field names
/// are stable across save files and tests.
/// </summary>
public static class Bestiary
{
    // ── Tier 1 — vermin of the upper halls (floors 1–2) ───────────────────────
    public static readonly MonsterTemplate GiantRat = new("Giant Rat", 4, 9, 1, 4, 0, 15, 2, 6, Speed: 3);
    public static readonly MonsterTemplate CaveBat = new("Cave Bat", 3, 6, 1, 4, 0, 12, 0, 6, Speed: 6);
    public static readonly MonsterTemplate Stirge = new("Stirge", 4, 7, 1, 4, 0, 14, 0, 5, Speed: 5);
    public static readonly MonsterTemplate Kobold = new("Kobold", 5, 8, 1, 6, 0, 20, 5, 5);
    public static readonly MonsterTemplate Goblin = new("Goblin", 6, 8, 1, 6, 0, 18, 6, 5);
    public static readonly MonsterTemplate GiantCentipede = new("Giant Centipede", 5, 8, 1, 4, 0, 16, 2, 4, Speed: 3,
        InflictsStatus: StatusEffect.Poisoned, StatusChance: 0.30);
    public static readonly MonsterTemplate GiantFrog = new("Giant Frog", 7, 8, 1, 4, 0, 16, 3, 4);
    public static readonly MonsterTemplate GrayOoze = new("Gray Ooze", 9, 9, 1, 4, 0, 18, 0, 3, Speed: 1);
    public static readonly MonsterTemplate SewerSnake = new("Sewer Snake", 5, 7, 1, 4, 0, 15, 1, 4, Speed: 4,
        InflictsStatus: StatusEffect.Poisoned, StatusChance: 0.25);
    public static readonly MonsterTemplate DustMephit = new("Dust Mephit", 6, 7, 1, 4, 0, 17, 2, 4, Speed: 4);

    // ── Tier 2 — denizens of the crypts (floors 3–4) ──────────────────────────
    public static readonly MonsterTemplate Skeleton = new("Skeleton", 8, 8, 1, 6, 0, 40, 8, 4);
    public static readonly MonsterTemplate Zombie = new("Zombie", 10, 9, 1, 6, 0, 22, 4, 4, Speed: 1);
    public static readonly MonsterTemplate GiantSpider = new("Giant Spider", 7, 7, 1, 4, 0, 35, 6, 4, Speed: 4,
        InflictsStatus: StatusEffect.Poisoned, StatusChance: 0.40);
    public static readonly MonsterTemplate Cutpurse = new("Cutpurse", 8, 8, 1, 4, 0, 35, 0, 4, Speed: 5,
        Ability: MonsterAbility.StealGold, AbilityChance: 0.50);
    public static readonly MonsterTemplate GiantAnt = new("Giant Ant", 9, 6, 1, 6, 0, 28, 2, 4);
    public static readonly MonsterTemplate Jackal = new("Jackal", 7, 7, 1, 6, 0, 24, 2, 5, Speed: 5);
    public static readonly MonsterTemplate Bandit = new("Bandit", 10, 7, 1, 6, 0, 30, 12, 4);
    public static readonly MonsterTemplate GiantWasp = new("Giant Wasp", 6, 6, 1, 4, 0, 26, 0, 4, Speed: 5,
        InflictsStatus: StatusEffect.Poisoned, StatusChance: 0.30);
    public static readonly MonsterTemplate Hobgoblin = new("Hobgoblin", 12, 7, 1, 8, 0, 42, 14, 4);
    public static readonly MonsterTemplate CultAcolyte = new("Cult Acolyte", 10, 8, 1, 6, 0, 38, 15, 3, Speed: 2,
        Spell: new MonsterSpell("Minor Mending", MonsterSpellKind.HealAllies, Power: 6, Chance: 0.45));

    // ── Tier 3 — warbands & beasts (floors 5–6) ───────────────────────────────
    public static readonly MonsterTemplate Orc = new("Orc", 14, 7, 1, 8, 0, 45, 15, 4);
    public static readonly MonsterTemplate Gnoll = new("Gnoll", 15, 7, 1, 8, 1, 52, 16, 3);
    public static readonly MonsterTemplate Lizardman = new("Lizardman", 14, 6, 1, 8, 0, 50, 12, 3);
    public static readonly MonsterTemplate GiantScorpion = new("Giant Scorpion", 13, 6, 1, 6, 0, 50, 8, 3, Speed: 3,
        InflictsStatus: StatusEffect.Poisoned, StatusChance: 0.35);
    public static readonly MonsterTemplate Harpy = new("Harpy", 12, 7, 1, 4, 0, 48, 10, 3, Speed: 4);
    public static readonly MonsterTemplate SparkImp = new("Spark Imp", 6, 7, 1, 3, 0, 50, 12, 3, Speed: 4,
        Spell: new MonsterSpell("Spark", MonsterSpellKind.BlastParty, Power: 5, Chance: 0.40));
    public static readonly MonsterTemplate Bugbear = new("Bugbear", 18, 6, 1, 8, 1, 58, 18, 3);
    public static readonly MonsterTemplate Worg = new("Worg", 16, 6, 1, 8, 0, 55, 6, 3, Speed: 5);
    public static readonly MonsterTemplate OrcArcher = new("Orc Archer", 12, 7, 1, 6, 1, 50, 14, 3, Speed: 3);
    public static readonly MonsterTemplate AnimatedArmor = new("Animated Armor", 18, 4, 1, 8, 1, 60, 22, 2, Speed: 1);

    // ── Tier 4 — haunts & hunters (floors 7–8) ────────────────────────────────
    public static readonly MonsterTemplate CovenWitch = new("Coven Witch", 10, 7, 1, 4, 0, 75, 25, 2, Speed: 3,
        Spell: new MonsterSpell("Slumber", MonsterSpellKind.SleepFoe, Power: 2, Chance: 0.45));
    public static readonly MonsterTemplate WillOWisp = new("Will-o-Wisp", 12, 4, 1, 4, 0, 80, 0, 2, Speed: 5,
        InflictsStatus: StatusEffect.Asleep, StatusChance: 0.35);
    public static readonly MonsterTemplate Ghoul = new("Ghoul", 16, 6, 1, 6, 1, 90, 18, 3, Speed: 2,
        InflictsStatus: StatusEffect.Paralyzed, StatusChance: 0.25);
    public static readonly MonsterTemplate Berserker = new("Berserker", 14, 7, 1, 8, 1, 70, 20, 3);
    public static readonly MonsterTemplate DireWolf = new("Dire Wolf", 20, 6, 1, 8, 0, 85, 10, 3, Speed: 5);
    public static readonly MonsterTemplate Ghast = new("Ghast", 20, 5, 1, 8, 0, 95, 20, 2, Speed: 2,
        InflictsStatus: StatusEffect.Paralyzed, StatusChance: 0.30);
    public static readonly MonsterTemplate Shadow = new("Shadow", 16, 5, 1, 6, 0, 90, 0, 3, Speed: 3,
        Ability: MonsterAbility.DrainStat, AbilityChance: 0.20);
    public static readonly MonsterTemplate HellHound = new("Hell Hound", 22, 5, 1, 8, 0, 100, 15, 2, Speed: 5);
    public static readonly MonsterTemplate Owlbear = new("Owlbear", 24, 6, 2, 6, 0, 95, 18, 2);
    public static readonly MonsterTemplate Cockatrice = new("Cockatrice", 18, 6, 1, 6, 0, 90, 15, 2, Speed: 3,
        InflictsStatus: StatusEffect.Paralyzed, StatusChance: 0.30);

    // ── Tier 5 — the cursed deep (floors 9–10) ────────────────────────────────
    public static readonly MonsterTemplate Gargoyle = new("Gargoyle", 22, 3, 1, 8, 0, 95, 25, 2);
    public static readonly MonsterTemplate MadGodAcolyte = new("Mad God Acolyte", 14, 6, 1, 6, 0, 95, 30, 2, Speed: 2,
        Spell: new MonsterSpell("Mending Chant", MonsterSpellKind.HealAllies, Power: 10, Chance: 0.55));
    public static readonly MonsterTemplate CryptCrawler = new("Crypt Crawler", 18, 7, 1, 6, 0, 100, 15, 2, Speed: 2,
        Ability: MonsterAbility.DrainStat, AbilityChance: 0.30);
    public static readonly MonsterTemplate DarkElf = new("Dark Elf", 16, 6, 1, 6, 0, 100, 30, 2, Speed: 3,
        Spell: new MonsterSpell("Shadow Bolt", MonsterSpellKind.DamageFoe, Power: 12, Chance: 0.50));
    public static readonly MonsterTemplate Werewolf = new("Werewolf", 24, 5, 1, 8, 2, 105, 20, 2, Speed: 4);
    public static readonly MonsterTemplate Wight = new("Wight", 22, 5, 1, 8, 0, 105, 28, 2, Speed: 2,
        Ability: MonsterAbility.DrainLevel, AbilityChance: 0.20);
    public static readonly MonsterTemplate DisplacerBeast = new("Displacer Beast", 26, 4, 2, 6, 0, 110, 20, 2, Speed: 4);
    public static readonly MonsterTemplate PhaseSpider = new("Phase Spider", 22, 5, 1, 8, 0, 108, 15, 2, Speed: 4,
        InflictsStatus: StatusEffect.Poisoned, StatusChance: 0.35);
    public static readonly MonsterTemplate Ettercap = new("Ettercap", 20, 6, 1, 6, 0, 100, 18, 2,
        InflictsStatus: StatusEffect.Poisoned, StatusChance: 0.30);
    public static readonly MonsterTemplate HexAdept = new("Hex Adept", 14, 6, 1, 4, 0, 120, 40, 2, Speed: 3,
        Spell: new MonsterSpell("Soul Bolt", MonsterSpellKind.DamageFoe, Power: 14, Chance: 0.55));

    // ── Tier 6 — giants & sorcery (floors 11–12) ──────────────────────────────
    public static readonly MonsterTemplate Ogre = new("Ogre", 28, 6, 2, 6, 0, 110, 30, 2);
    public static readonly MonsterTemplate Minotaur = new("Minotaur", 30, 5, 2, 6, 0, 130, 35, 1);
    public static readonly MonsterTemplate Wraith = new("Wraith", 20, 5, 2, 6, 2, 150, 30, 2, Speed: 2,
        Ability: MonsterAbility.DrainLevel, AbilityChance: 0.30);
    public static readonly MonsterTemplate FlameShade = new("Flame Shade", 16, 5, 1, 6, 0, 130, 35, 2, Speed: 3,
        Spell: new MonsterSpell("Cinderblast", MonsterSpellKind.BlastParty, Power: 9, Chance: 0.50));
    public static readonly MonsterTemplate Necromancer = new("Necromancer", 16, 6, 1, 4, 0, 130, 30, 1, Speed: 2,
        Spell: new MonsterSpell("Summon Dead", MonsterSpellKind.Summon, Power: 2, Chance: 0.40, SummonTemplate: Skeleton));
    public static readonly MonsterTemplate Ettin = new("Ettin", 38, 5, 2, 8, 0, 170, 40, 1);
    public static readonly MonsterTemplate Cyclops = new("Cyclops", 40, 5, 2, 8, 1, 180, 45, 1);
    public static readonly MonsterTemplate Salamander = new("Salamander", 32, 4, 2, 6, 0, 160, 30, 1, Speed: 3);
    public static readonly MonsterTemplate Gorgon = new("Gorgon", 36, 3, 2, 6, 0, 165, 35, 1,
        InflictsStatus: StatusEffect.Paralyzed, StatusChance: 0.30);
    public static readonly MonsterTemplate SpiritNaga = new("Spirit Naga", 30, 5, 1, 8, 0, 170, 40, 1, Speed: 3,
        Spell: new MonsterSpell("Mind Lash", MonsterSpellKind.DamageFoe, Power: 14, Chance: 0.50));

    // ── Tier 7 — monsters of legend (floors 13–14) ────────────────────────────
    public static readonly MonsterTemplate Basilisk = new("Basilisk", 34, 4, 1, 8, 0, 180, 30, 1,
        InflictsStatus: StatusEffect.Paralyzed, StatusChance: 0.35);
    public static readonly MonsterTemplate Banshee = new("Banshee", 30, 5, 1, 8, 0, 180, 40, 1, Speed: 3,
        Spell: new MonsterSpell("Keening Wail", MonsterSpellKind.BlastParty, Power: 8, Chance: 0.50));
    public static readonly MonsterTemplate Manticore = new("Manticore", 36, 5, 2, 6, 0, 185, 30, 1, Speed: 4);
    public static readonly MonsterTemplate Wyvern = new("Wyvern", 40, 4, 2, 6, 0, 190, 35, 1, Speed: 4,
        InflictsStatus: StatusEffect.Poisoned, StatusChance: 0.30);
    public static readonly MonsterTemplate Troll = new("Troll", 45, 4, 2, 6, 0, 200, 40, 1);
    public static readonly MonsterTemplate HillGiant = new("Hill Giant", 48, 5, 2, 8, 0, 200, 55, 1);
    public static readonly MonsterTemplate StoneGolem = new("Stone Golem", 50, 2, 2, 8, 0, 210, 50, 1, Speed: 1);
    public static readonly MonsterTemplate YoungBlueDragon = new("Young Blue Dragon", 52, 3, 2, 8, 0, 240, 120, 1, Speed: 4,
        Spell: new MonsterSpell("Lightning Breath", MonsterSpellKind.BlastParty, Power: 14, Chance: 0.50));
    public static readonly MonsterTemplate BoneGolem = new("Bone Golem", 48, 3, 2, 6, 0, 200, 30, 1, Speed: 1);
    public static readonly MonsterTemplate WraithLord = new("Wraith Lord", 42, 3, 2, 6, 0, 230, 60, 1, Speed: 3,
        Ability: MonsterAbility.DrainLevel, AbilityChance: 0.30);

    // ── Tier 8 — terrors of the abyss (floors 15–16) ──────────────────────────
    public static readonly MonsterTemplate Vampire = new("Vampire", 38, 3, 1, 10, 0, 220, 60, 1, Speed: 3,
        Ability: MonsterAbility.DrainLevel, AbilityChance: 0.25);
    public static readonly MonsterTemplate Chimera = new("Chimera", 44, 4, 2, 8, 0, 230, 45, 1);
    public static readonly MonsterTemplate FrostGiant = new("Frost Giant", 60, 4, 3, 6, 0, 320, 90, 1);
    public static readonly MonsterTemplate EyeTyrant = new("Eye Tyrant", 50, 2, 1, 8, 0, 350, 100, 1, Speed: 2,
        Spell: new MonsterSpell("Disintegrate Ray", MonsterSpellKind.DamageFoe, Power: 20, Chance: 0.55));
    public static readonly MonsterTemplate IronGolem = new("Iron Golem", 75, 1, 2, 8, 0, 360, 40, 1, Speed: 1);
    public static readonly MonsterTemplate StormGiant = new("Storm Giant", 70, 3, 3, 6, 0, 380, 110, 1, Speed: 3,
        Spell: new MonsterSpell("Thunderclap", MonsterSpellKind.BlastParty, Power: 16, Chance: 0.50));
    public static readonly MonsterTemplate Nightwalker = new("Nightwalker", 65, 2, 2, 8, 0, 360, 50, 1, Speed: 3,
        Ability: MonsterAbility.DrainLevel, AbilityChance: 0.30);
    public static readonly MonsterTemplate Roc = new("Roc", 70, 4, 3, 6, 0, 340, 60, 1, Speed: 5);
    public static readonly MonsterTemplate Beholder = new("Beholder", 55, 1, 1, 8, 0, 400, 150, 1, Speed: 2,
        Spell: new MonsterSpell("Death Ray", MonsterSpellKind.DamageFoe, Power: 24, Chance: 0.55));
    public static readonly MonsterTemplate MindFlayer = new("Mind Flayer", 48, 3, 1, 8, 0, 360, 120, 1, Speed: 2,
        Spell: new MonsterSpell("Mind Blast", MonsterSpellKind.BlastParty, Power: 14, Chance: 0.55),
        Ability: MonsterAbility.DrainStat, AbilityChance: 0.25);

    // ── Tier 9 — the great wyrms & fiends (floors 17–18) ──────────────────────
    public static readonly MonsterTemplate Hydra = new("Hydra", 75, 4, 3, 6, 0, 360, 80, 1);
    public static readonly MonsterTemplate StormDrake = new("Storm Drake", 68, 3, 3, 6, 0, 380, 120, 1, Speed: 4,
        Spell: new MonsterSpell("Lightning Breath", MonsterSpellKind.BlastParty, Power: 15, Chance: 0.50));
    public static readonly MonsterTemplate Lich = new("Lich", 55, 3, 1, 10, 0, 380, 150, 1, Speed: 2,
        Spell: new MonsterSpell("Raise Dead", MonsterSpellKind.Summon, Power: 3, Chance: 0.45, SummonTemplate: Skeleton),
        Ability: MonsterAbility.DrainLevel, AbilityChance: 0.25);
    public static readonly MonsterTemplate YoungRedDragon = new("Young Red Dragon", 70, 2, 3, 6, 0, 400, 200, 1, Speed: 4,
        Spell: new MonsterSpell("Fire Breath", MonsterSpellKind.BlastParty, Power: 16, Chance: 0.55));
    public static readonly MonsterTemplate PitFiend = new("Pit Fiend", 65, 2, 2, 8, 0, 420, 120, 1, Speed: 3,
        Spell: new MonsterSpell("Hellfire", MonsterSpellKind.BlastParty, Power: 14, Chance: 0.50));
    public static readonly MonsterTemplate DeathKnight = new("Death Knight", 58, 2, 2, 8, 0, 340, 110, 1, Speed: 2,
        Ability: MonsterAbility.DrainLevel, AbilityChance: 0.30);
    public static readonly MonsterTemplate AdultWhiteDragon = new("Adult White Dragon", 110, 1, 3, 8, 0, 550, 250, 1, Speed: 4,
        Spell: new MonsterSpell("Frost Breath", MonsterSpellKind.BlastParty, Power: 18, Chance: 0.55));
    public static readonly MonsterTemplate Balor = new("Balor", 100, 0, 3, 6, 0, 600, 200, 1, Speed: 4,
        Spell: new MonsterSpell("Fire Whip", MonsterSpellKind.BlastParty, Power: 18, Chance: 0.50),
        Ability: MonsterAbility.DrainLevel, AbilityChance: 0.25);
    public static readonly MonsterTemplate Kraken = new("Kraken", 130, 2, 3, 8, 0, 580, 150, 1, Speed: 2);
    public static readonly MonsterTemplate ElderVampire = new("Elder Vampire", 90, 1, 2, 8, 0, 520, 180, 1, Speed: 4,
        Ability: MonsterAbility.DrainLevel, AbilityChance: 0.35);

    // ── Tier 10 — the mythic horrors of the abyss floor (floors 19–20) ────────
    public static readonly MonsterTemplate AncientRedDragon = new("Ancient Red Dragon", 200, 0, 4, 8, 0, 1200, 600, 1, Speed: 4,
        Spell: new MonsterSpell("Inferno", MonsterSpellKind.BlastParty, Power: 24, Chance: 0.60));
    public static readonly MonsterTemplate AncientBlackDragon = new("Ancient Black Dragon", 180, 0, 4, 6, 0, 1000, 500, 1, Speed: 4,
        Spell: new MonsterSpell("Acid Breath", MonsterSpellKind.BlastParty, Power: 22, Chance: 0.55),
        InflictsStatus: StatusEffect.Poisoned, StatusChance: 0.30);
    public static readonly MonsterTemplate DemonPrince = new("Demon Prince", 190, 0, 3, 8, 0, 1100, 550, 1, Speed: 4,
        Spell: new MonsterSpell("Hellstorm", MonsterSpellKind.BlastParty, Power: 26, Chance: 0.55),
        Ability: MonsterAbility.DrainLevel, AbilityChance: 0.30);
    public static readonly MonsterTemplate Tarrasque = new("Tarrasque", 260, 2, 4, 8, 0, 1500, 200, 1, Speed: 2);
    public static readonly MonsterTemplate ElderLich = new("Elder Lich", 150, 0, 2, 10, 0, 1000, 500, 1, Speed: 2,
        Spell: new MonsterSpell("Soul Reap", MonsterSpellKind.DamageFoe, Power: 30, Chance: 0.60),
        Ability: MonsterAbility.DrainLevel, AbilityChance: 0.35);
    public static readonly MonsterTemplate Titan = new("Titan", 220, 1, 4, 6, 0, 1100, 500, 1, Speed: 3);
    public static readonly MonsterTemplate ElderBrain = new("Elder Brain", 140, 0, 1, 10, 0, 1000, 400, 1, Speed: 1,
        Spell: new MonsterSpell("Psychic Storm", MonsterSpellKind.BlastParty, Power: 24, Chance: 0.60),
        Ability: MonsterAbility.DrainStat, AbilityChance: 0.30);
    public static readonly MonsterTemplate VoidHorror = new("Void Horror", 160, 0, 3, 8, 0, 1050, 300, 1, Speed: 2,
        Spell: new MonsterSpell("Unmaking", MonsterSpellKind.DamageFoe, Power: 32, Chance: 0.55));
    public static readonly MonsterTemplate ArchDevil = new("Arch-Devil", 170, 0, 3, 8, 0, 1100, 600, 1, Speed: 3,
        Spell: new MonsterSpell("Cataclysm", MonsterSpellKind.BlastParty, Power: 24, Chance: 0.55),
        Ability: MonsterAbility.DrainLevel, AbilityChance: 0.30);
    public static readonly MonsterTemplate DeathWyrm = new("Death Wyrm", 210, 1, 4, 8, 0, 1200, 550, 1, Speed: 3,
        Spell: new MonsterSpell("Necrotic Breath", MonsterSpellKind.BlastParty, Power: 26, Chance: 0.55),
        Ability: MonsterAbility.DrainLevel, AbilityChance: 0.30);

    // ── Toughness tiers (used to populate encounters by depth) ────────────────
    public static readonly IReadOnlyList<MonsterTemplate> Tier1 =
        new[] { GiantRat, CaveBat, Stirge, Kobold, Goblin, GiantCentipede, GiantFrog, GrayOoze, SewerSnake, DustMephit };
    public static readonly IReadOnlyList<MonsterTemplate> Tier2 =
        new[] { Skeleton, Zombie, GiantSpider, Cutpurse, GiantAnt, Jackal, Bandit, GiantWasp, Hobgoblin, CultAcolyte };
    public static readonly IReadOnlyList<MonsterTemplate> Tier3 =
        new[] { Orc, Gnoll, Lizardman, GiantScorpion, Harpy, SparkImp, Bugbear, Worg, OrcArcher, AnimatedArmor };
    public static readonly IReadOnlyList<MonsterTemplate> Tier4 =
        new[] { CovenWitch, WillOWisp, Ghoul, Berserker, DireWolf, Ghast, Shadow, HellHound, Owlbear, Cockatrice };
    public static readonly IReadOnlyList<MonsterTemplate> Tier5 =
        new[] { Gargoyle, MadGodAcolyte, CryptCrawler, DarkElf, Werewolf, Wight, DisplacerBeast, PhaseSpider, Ettercap, HexAdept };
    public static readonly IReadOnlyList<MonsterTemplate> Tier6 =
        new[] { Ogre, Minotaur, Wraith, FlameShade, Necromancer, Ettin, Cyclops, Salamander, Gorgon, SpiritNaga };
    public static readonly IReadOnlyList<MonsterTemplate> Tier7 =
        new[] { Basilisk, Banshee, Manticore, Wyvern, Troll, HillGiant, StoneGolem, YoungBlueDragon, BoneGolem, WraithLord };
    public static readonly IReadOnlyList<MonsterTemplate> Tier8 =
        new[] { Vampire, Chimera, FrostGiant, EyeTyrant, IronGolem, StormGiant, Nightwalker, Roc, Beholder, MindFlayer };
    public static readonly IReadOnlyList<MonsterTemplate> Tier9 =
        new[] { Hydra, StormDrake, Lich, YoungRedDragon, PitFiend, DeathKnight, AdultWhiteDragon, Balor, Kraken, ElderVampire };
    public static readonly IReadOnlyList<MonsterTemplate> Tier10 =
        new[] { AncientRedDragon, AncientBlackDragon, DemonPrince, Tarrasque, ElderLich, Titan, ElderBrain, VoidHorror, ArchDevil, DeathWyrm };

    private static readonly IReadOnlyList<MonsterTemplate>[] Tiers =
        { Tier1, Tier2, Tier3, Tier4, Tier5, Tier6, Tier7, Tier8, Tier9, Tier10 };

    /// <summary>Every wandering monster, weakest to strongest — the bestiary roster (bosses aside).</summary>
    public static readonly IReadOnlyList<MonsterTemplate> AllWandering = Tiers.SelectMany(t => t).ToArray();

    // Quest pools: townsfolk send you after common foes; strangers want relics from the deep.
    public static readonly IReadOnlyList<MonsterTemplate> Common =
        Tier1.Concat(Tier2).Concat(Tier3).Concat(Tier4).ToArray();
    public static readonly IReadOnlyList<MonsterTemplate> Tough =
        Tier6.Concat(Tier7).Concat(Tier8).Concat(Tier9).Concat(Tier10).ToArray();

    // A floor draws from its tier plus the one below it, for variety.
    private static readonly IReadOnlyList<MonsterTemplate>[] Bands = BuildBands();

    private static IReadOnlyList<MonsterTemplate>[] BuildBands()
    {
        var bands = new IReadOnlyList<MonsterTemplate>[Tiers.Length];
        for (var i = 0; i < Tiers.Length; i++)
            bands[i] = i == 0 ? Tiers[0] : Tiers[i].Concat(Tiers[i - 1]).ToArray();
        return bands;
    }

    /// <summary>The wandering-monster pool for a dungeon depth — two floors per tier, ramping up.</summary>
    public static IReadOnlyList<MonsterTemplate> PoolForDepth(int depth)
    {
        var tier = Math.Clamp((depth + 1) / 2, 1, Tiers.Length);
        return Bands[tier - 1];
    }
}
