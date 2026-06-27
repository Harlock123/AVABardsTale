using System;

namespace BardsTale.Core.Game;

/// <summary>
/// Opt-in challenge mutators chosen at new-game time, composable with difficulty and New Game+.
/// Each makes the run harder in a distinct way and raises the run's score.
/// </summary>
[Flags]
public enum RunModifier
{
    None = 0,
    /// <summary>Garth's Equipment Shoppe is boarded up — no buying, selling or identifying.</summary>
    NoShops = 1 << 0,
    /// <summary>Wandering monsters hound the party far more often.</summary>
    Relentless = 1 << 1,
    /// <summary>The party cannot make camp in the dungeon — recover only back in town.</summary>
    NoCamp = 1 << 2,
    /// <summary>Half the gold and experience from every fight.</summary>
    Pauper = 1 << 3,
    /// <summary>All treasure is found unidentified — pay to learn what it is.</summary>
    Cursed = 1 << 4
}

/// <summary>The aggregate effects of the active <see cref="RunModifier"/> flags.</summary>
public static class RunModifiers
{
    /// <summary>Every toggleable modifier, for the settings UI.</summary>
    public static readonly RunModifier[] All =
        { RunModifier.NoShops, RunModifier.Relentless, RunModifier.NoCamp, RunModifier.Pauper, RunModifier.Cursed };

    public static bool Has(RunModifier set, RunModifier flag) => (set & flag) != 0;

    /// <summary>Multiplier on the wandering-encounter rate.</summary>
    public static double EncounterChance(RunModifier m) => Has(m, RunModifier.Relentless) ? 1.7 : 1.0;

    /// <summary>Multiplier on XP and gold rewards.</summary>
    public static double Reward(RunModifier m) => Has(m, RunModifier.Pauper) ? 0.5 : 1.0;

    public static bool ShopsOpen(RunModifier m) => !Has(m, RunModifier.NoShops);
    public static bool CampAllowed(RunModifier m) => !Has(m, RunModifier.NoCamp);
    public static bool LootUnidentified(RunModifier m) => Has(m, RunModifier.Cursed);

    /// <summary>How many modifiers are active.</summary>
    public static int Count(RunModifier m)
    {
        var n = 0;
        foreach (var flag in All)
            if (Has(m, flag)) n++;
        return n;
    }

    /// <summary>A score bonus for playing with mutators — +20% per active modifier.</summary>
    public static double ScoreMultiplier(RunModifier m) => 1.0 + 0.20 * Count(m);

    /// <summary>A short, human-readable name for a single modifier.</summary>
    public static string Name(RunModifier flag) => flag switch
    {
        RunModifier.NoShops => "No Shops",
        RunModifier.Relentless => "Relentless",
        RunModifier.NoCamp => "No Camp",
        RunModifier.Pauper => "Pauper",
        RunModifier.Cursed => "Cursed Loot",
        _ => flag.ToString()
    };

    /// <summary>A one-line description of what a modifier does.</summary>
    public static string Describe(RunModifier flag) => flag switch
    {
        RunModifier.NoShops => "Garth's shop is boarded up — no buying, selling or identifying.",
        RunModifier.Relentless => "Wandering monsters ambush the party far more often.",
        RunModifier.NoCamp => "You cannot make camp in the dungeon — recover only in town.",
        RunModifier.Pauper => "Half the gold and experience from every fight.",
        RunModifier.Cursed => "All treasure is found unidentified — pay to learn what it is.",
        _ => ""
    };
}
