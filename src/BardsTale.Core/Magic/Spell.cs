using BardsTale.Core.Characters;
using BardsTale.Core.Combat;

namespace BardsTale.Core.Magic;

public enum SpellEffect
{
    DamageEnemy,
    DamageAllEnemies,
    HealAlly,
    HealParty,
    RestoreLight,
    Revive,
    BuffPartyArmor,
    BuffPartyAttack,
    CureStatus,
    Identify,
    /// <summary>Grants the party extra attacks each round for the fight (Power = extra swings).</summary>
    HasteParty,
    /// <summary>Heals the party a little at the end of every round for the fight (Power = HP).</summary>
    RegenParty,
    /// <summary>Cures every ailment from the whole party at once.</summary>
    CleanseParty,
    /// <summary>Restores spell points to the whole party (Power = SP).</summary>
    RestorePartySpellPoints,
    /// <summary>Damages one enemy and heals the caster for part of the harm done.</summary>
    DrainEnemy,
    /// <summary>Calls every fallen ally back to life at once (Power = HP each is revived with).</summary>
    ReviveParty,
    /// <summary>Lulls an enemy group to sleep — they skip turns until they wake or are struck (Power = rounds).</summary>
    SleepEnemies,
    /// <summary>Poisons an enemy group, sapping their health each round (Power = rounds).</summary>
    PoisonEnemies,
    /// <summary>Scrying: reveals one foe's elemental weaknesses, resistances and immunities. Deals no damage.</summary>
    RevealLore,
    /// <summary>Clairvoyance: out in the maze, reveals the surrounding cells and their hidden trickery on the auto-map (Power = radius).</summary>
    RevealArea
}

public enum SpellTarget
{
    None,
    SingleEnemy,
    AllEnemies,
    SingleAlly,
    Party
}

/// <summary>
/// A castable spell. <see cref="Level"/> is the caster level required to learn it,
/// so spell lists grow as a mage advances at the Review Board. Four-letter codes
/// echo the original game's spell shorthand.
/// </summary>
public sealed record Spell(
    string Id,
    string Code,
    string Name,
    MagicSchool School,
    int Level,
    int Cost,
    SpellEffect Effect,
    SpellTarget Target,
    int Power,
    string Description,
    Element Element = Element.Arcane)
{
    public bool TargetsEnemies => Target is SpellTarget.SingleEnemy or SpellTarget.AllEnemies;
    public bool TargetsAllies => Target is SpellTarget.SingleAlly or SpellTarget.Party;

    /// <summary>Utility spells like Identify and Clairvoyance have no place in a combat round.</summary>
    public bool UsableInCombat => Effect is not (SpellEffect.Identify or SpellEffect.RevealArea);

    /// <summary>Restorative spells the party can cast on themselves between fights, in town.</summary>
    public bool UsableInTown => Effect is SpellEffect.HealAlly or SpellEffect.HealParty
        or SpellEffect.CureStatus or SpellEffect.Revive or SpellEffect.ReviveParty;

    public string Summary => Effect switch
    {
        SpellEffect.DamageEnemy or SpellEffect.DamageAllEnemies => $"{Cost} SP · ~{Power} dmg",
        SpellEffect.DrainEnemy => $"{Cost} SP · ~{Power} dmg, heal self",
        SpellEffect.HealAlly or SpellEffect.HealParty => $"{Cost} SP · ~{Power} heal",
        SpellEffect.BuffPartyArmor => $"{Cost} SP · party AC +{Power}",
        SpellEffect.BuffPartyAttack => $"{Cost} SP · party hits +{Power}",
        SpellEffect.Revive => $"{Cost} SP · revive",
        SpellEffect.CureStatus or SpellEffect.CleanseParty => $"{Cost} SP · cure ailments",
        SpellEffect.HasteParty => $"{Cost} SP · party +{Power} attack/round",
        SpellEffect.RegenParty => $"{Cost} SP · party regen {Power}/round",
        SpellEffect.RestorePartySpellPoints => $"{Cost} SP · party +{Power} SP",
        SpellEffect.ReviveParty => $"{Cost} SP · revive all fallen",
        SpellEffect.SleepEnemies => $"{Cost} SP · sleep a foe group",
        SpellEffect.PoisonEnemies => $"{Cost} SP · poison a foe group",
        SpellEffect.Identify => $"{Cost} SP · identify an item",
        SpellEffect.RevealLore => $"{Cost} SP · reveal a foe's affinities",
        SpellEffect.RevealArea => $"{Cost} SP · scry the area",
        _ => $"{Cost} SP"
    };
}

public static class Spells
{
    public static readonly IReadOnlyList<Spell> All = new[]
    {
        // --- Conjurer: healing & protection ---
        new Spell("VOPL", "VOPL", "Vorpal Plating", MagicSchool.Conjurer, 1, 2, SpellEffect.HealAlly, SpellTarget.SingleAlly, 8,
            "Knits flesh and bone, mending a single companion."),
        new Spell("AROF", "AROF", "Armour of Frost", MagicSchool.Conjurer, 1, 3, SpellEffect.BuffPartyArmor, SpellTarget.Party, 2,
            "Sheathes the party in rime, turning aside blows."),
        new Spell("HEPA", "HEPA", "Healing Spring", MagicSchool.Conjurer, 2, 5, SpellEffect.HealParty, SpellTarget.Party, 6,
            "A restorative mist heals the entire party."),
        new Spell("PURE", "PURE", "Purify", MagicSchool.Conjurer, 2, 4, SpellEffect.CureStatus, SpellTarget.SingleAlly, 0,
            "Cleanses poison, paralysis and sleep from a companion."),
        new Spell("REST", "REST", "Restoration", MagicSchool.Conjurer, 3, 8, SpellEffect.Revive, SpellTarget.SingleAlly, 1,
            "Calls a fallen companion back from death."),
        new Spell("RETI", "RETI", "Renewing Tide", MagicSchool.Conjurer, 3, 6, SpellEffect.RegenParty, SpellTarget.Party, 6,
            "A tide of life washes the party each round of the fight."),
        new Spell("MACL", "MACL", "Mass Cleansing", MagicSchool.Conjurer, 3, 5, SpellEffect.CleanseParty, SpellTarget.Party, 0,
            "Purges poison, sleep and paralysis from the whole party."),
        new Spell("SANC", "SANC", "Sanctuary", MagicSchool.Conjurer, 4, 9, SpellEffect.HealParty, SpellTarget.Party, 16,
            "A blaze of holy light closes the party's every wound."),
        new Spell("RESF", "RESF", "Resurrection Field", MagicSchool.Conjurer, 5, 16, SpellEffect.ReviveParty, SpellTarget.Party, 12,
            "Life surges through the dead — every fallen companion rises at once."),

        // --- Magician: fire & utility ---
        new Spell("MAFL", "MAFL", "Mage Flame", MagicSchool.Magician, 1, 1, SpellEffect.RestoreLight, SpellTarget.None, 0,
            "Conjures a magical light to pierce the gloom."),
        new Spell("ARFI", "ARFI", "Arc Fire", MagicSchool.Magician, 1, 2, SpellEffect.DamageEnemy, SpellTarget.SingleEnemy, 8,
            "Hurls a bolt of fire at a single foe.", Element.Fire),
        new Spell("SCSI", "SCSI", "Scrye Sight", MagicSchool.Magician, 1, 2, SpellEffect.Identify, SpellTarget.None, 0,
            "Reveals the true nature of an unidentified item."),
        new Spell("SCFO", "SCFO", "Scrye Foe", MagicSchool.Magician, 1, 1, SpellEffect.RevealLore, SpellTarget.SingleEnemy, 0,
            "Lays bare a foe's nature — the elements it shrugs off, and the elements that savage it."),
        new Spell("CLAI", "CLAI", "Clairvoyance", MagicSchool.Magician, 2, 3, SpellEffect.RevealArea, SpellTarget.None, 2,
            "Out in the maze, floods the surrounding stone with sight — mapping nearby cells and the illusions, teleporters and traps among them."),
        new Spell("MAFO", "MAFO", "Mana Font", MagicSchool.Magician, 3, 3, SpellEffect.RestorePartySpellPoints, SpellTarget.Party, 10,
            "Channels arcane vigour, restoring spell points to the whole party."),
        new Spell("FROS", "FROS", "Frost Blast", MagicSchool.Magician, 2, 3, SpellEffect.DamageEnemy, SpellTarget.SingleEnemy, 13,
            "A lance of cold transfixes an enemy.", Element.Cold),
        new Spell("VESP", "VESP", "Venom Spray", MagicSchool.Magician, 2, 4, SpellEffect.PoisonEnemies, SpellTarget.SingleEnemy, 4,
            "A cloud of venom engulfs a foe group, rotting them from within.", Element.Poison),
        new Spell("FIHO", "FIHO", "Fire Horn", MagicSchool.Magician, 3, 5, SpellEffect.DamageAllEnemies, SpellTarget.AllEnemies, 8,
            "Looses a blast of flame across an enemy group.", Element.Fire),
        new Spell("FIST", "FIST", "Firestorm", MagicSchool.Magician, 4, 8, SpellEffect.DamageAllEnemies, SpellTarget.AllEnemies, 14,
            "A roaring tempest of fire engulfs an enemy group.", Element.Fire),
        new Spell("METE", "METE", "Meteor Swarm", MagicSchool.Magician, 5, 12, SpellEffect.DamageAllEnemies, SpellTarget.AllEnemies, 22,
            "Burning stones rain from the dark, hammering every foe.", Element.Fire),

        // --- Sorcerer: force & mind ---
        new Spell("FOFO", "FOFO", "Force Focus", MagicSchool.Sorcerer, 1, 3, SpellEffect.DamageEnemy, SpellTarget.SingleEnemy, 12,
            "Crushes one enemy with focused force."),
        new Spell("BASP", "BASP", "Battle Spirit", MagicSchool.Sorcerer, 2, 4, SpellEffect.BuffPartyAttack, SpellTarget.Party, 2,
            "Fills the party with martial fervour, sharpening their blows."),
        new Spell("SOLE", "SOLE", "Soul Leech", MagicSchool.Sorcerer, 2, 5, SpellEffect.DrainEnemy, SpellTarget.SingleEnemy, 16,
            "Rips life from a foe and pours it into the caster."),
        new Spell("QUBL", "QUBL", "Quicken Blood", MagicSchool.Sorcerer, 3, 8, SpellEffect.HasteParty, SpellTarget.Party, 1,
            "Hastens the party's blood, granting an extra strike each round."),
        new Spell("MIJA", "MIJA", "Mind Jab", MagicSchool.Sorcerer, 2, 5, SpellEffect.DamageEnemy, SpellTarget.SingleEnemy, 17,
            "A spike of pure thought lances an enemy's mind."),
        new Spell("MIFO", "MIFO", "Mind Fog", MagicSchool.Sorcerer, 3, 6, SpellEffect.SleepEnemies, SpellTarget.SingleEnemy, 3,
            "A smothering fog lulls a foe group into helpless slumber."),
        new Spell("PSST", "PSST", "Psychic Storm", MagicSchool.Sorcerer, 3, 7, SpellEffect.DamageAllEnemies, SpellTarget.AllEnemies, 10,
            "Tears through an enemy group with raw psychic force."),
        new Spell("EGOW", "EGOW", "Ego Whip", MagicSchool.Sorcerer, 4, 7, SpellEffect.DrainEnemy, SpellTarget.SingleEnemy, 26,
            "Flays a foe's mind, feeding the stolen vigour to the caster."),
        new Spell("TEMP", "TEMP", "Temporal Surge", MagicSchool.Sorcerer, 5, 14, SpellEffect.HasteParty, SpellTarget.Party, 2,
            "Time bends to the party's will — two extra strikes each round."),

        // --- Wizard: raw destruction ---
        new Spell("MABL", "MABL", "Mage's Bolt", MagicSchool.Wizard, 1, 3, SpellEffect.DamageEnemy, SpellTarget.SingleEnemy, 14,
            "A searing bolt of arcane energy."),
        new Spell("CONF", "CONF", "Conflagration", MagicSchool.Wizard, 1, 6, SpellEffect.DamageAllEnemies, SpellTarget.AllEnemies, 7,
            "Engulfs an entire enemy group in flame.", Element.Fire),
        new Spell("REVI", "REVI", "Revival", MagicSchool.Wizard, 2, 7, SpellEffect.Revive, SpellTarget.SingleAlly, 1,
            "Wrenches a fallen ally back to life."),
        new Spell("DETH", "DETH", "Death Strike", MagicSchool.Wizard, 3, 9, SpellEffect.DamageEnemy, SpellTarget.SingleEnemy, 30,
            "Annihilating force obliterates a single foe."),
        new Spell("CHLI", "CHLI", "Chain Lightning", MagicSchool.Wizard, 4, 9, SpellEffect.DamageAllEnemies, SpellTarget.AllEnemies, 14,
            "Arcs of lightning leap from foe to foe across the group.", Element.Lightning),
        new Spell("ANNI", "ANNI", "Annihilation", MagicSchool.Wizard, 5, 14, SpellEffect.DamageEnemy, SpellTarget.SingleEnemy, 46,
            "A word of unmaking erases a single foe from existence."),
    };

    private static readonly Dictionary<string, Spell> ById = All.ToDictionary(s => s.Id);

    public static Spell Get(string id) => ById[id];

    /// <summary>Every spell of a school the caster is high-enough level to know.</summary>
    public static IEnumerable<Spell> KnownAtLevel(MagicSchool school, int level)
        => All.Where(s => s.School == school && s.Level <= level);

    /// <summary>Spells of a school that become available exactly at <paramref name="level"/>.</summary>
    public static IEnumerable<Spell> LearnedAtLevel(MagicSchool school, int level)
        => All.Where(s => s.School == school && s.Level == level);
}
