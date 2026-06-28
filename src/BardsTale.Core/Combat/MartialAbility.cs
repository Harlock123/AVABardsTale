using BardsTale.Core.Characters;

namespace BardsTale.Core.Combat;

/// <summary>
/// A once-per-fight signature manoeuvre that distinguishes the martial (non-casting) classes,
/// the way spells distinguish the mages and songs the Bard. Physical, so they work even in an
/// anti-magic zone.
/// </summary>
public enum MartialAbility
{
    None,
    /// <summary>Warrior — a sweeping blow that strikes <em>every</em> monster in the target group.</summary>
    Cleave,
    /// <summary>Paladin — a guaranteed radiant strike for double weapon damage, ignoring resistance.</summary>
    Smite,
    /// <summary>Rogue — a guaranteed strike for triple weapon damage; usable from any rank.</summary>
    Backstab,
    /// <summary>Hunter — a guaranteed ranged shot for double damage that may instantly fell a non-boss; any rank.</summary>
    CalledShot,
    /// <summary>Monk — a strike that, on landing, can stun (sleep) the target for a couple of rounds.</summary>
    StunningStrike
}

/// <summary>The martial-ability catalogue: which class gets which manoeuvre, and how it reads.</summary>
public static class MartialAbilities
{
    /// <summary>The signature ability of a class, or <see cref="MartialAbility.None"/> for casters and the Bard.</summary>
    public static MartialAbility For(CharacterClass cls) => cls switch
    {
        CharacterClass.Warrior => MartialAbility.Cleave,
        CharacterClass.Paladin => MartialAbility.Smite,
        CharacterClass.Rogue => MartialAbility.Backstab,
        CharacterClass.Hunter => MartialAbility.CalledShot,
        CharacterClass.Monk => MartialAbility.StunningStrike,
        _ => MartialAbility.None
    };

    /// <summary>Backstab and Called Shot are skirmisher moves — usable from the back rank too.</summary>
    public static bool IsRanged(MartialAbility a) => a is MartialAbility.Backstab or MartialAbility.CalledShot;

    public static string Name(MartialAbility a) => a switch
    {
        MartialAbility.Cleave => "Cleave",
        MartialAbility.Smite => "Smite",
        MartialAbility.Backstab => "Backstab",
        MartialAbility.CalledShot => "Called Shot",
        MartialAbility.StunningStrike => "Stunning Strike",
        _ => ""
    };

    public static string Detail(MartialAbility a) => a switch
    {
        MartialAbility.Cleave => "once per fight · strike every foe in a group",
        MartialAbility.Smite => "once per fight · a radiant strike for double damage",
        MartialAbility.Backstab => "once per fight · triple damage to one foe",
        MartialAbility.CalledShot => "once per fight · double damage, may instantly fell a lesser foe",
        MartialAbility.StunningStrike => "once per fight · a strike that may stun the foe",
        _ => ""
    };
}
