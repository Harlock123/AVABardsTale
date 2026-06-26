namespace BardsTale.Core.Combat;

/// <summary>The chosen challenge level for a run, picked at new-game time (like Ironman).</summary>
public enum Difficulty
{
    /// <summary>A gentler descent: frailer foes, rarer ambushes, safer camps.</summary>
    Relaxed,
    /// <summary>The classic Bard's Tale balance.</summary>
    Normal,
    /// <summary>Tougher, more frequent foes and riskier camps — but richer rewards.</summary>
    Hard
}

/// <summary>
/// The set of multipliers a <see cref="Difficulty"/> applies across the game: how sturdy and
/// dangerous monsters are, how often the party is ambushed, how rich the spoils, and how risky
/// it is to camp. Normal is the identity profile (all multipliers 1, no flat bonuses).
/// </summary>
public sealed record DifficultyProfile(
    Difficulty Level,
    string Name,
    double MonsterHp,       // multiplier on monster max HP
    int MonsterAttackBonus, // flat bonus to monster to-hit and damage
    double EncounterChance, // multiplier on the wandering-encounter frequency
    double Reward,          // multiplier on XP and gold gained
    double CampRisk,        // multiplier on the camp-ambush chance
    string Blurb)
{
    /// <summary>True for Normal — the baseline, where scaling is a no-op.</summary>
    public bool IsBaseline => Level == Difficulty.Normal;

    public static DifficultyProfile For(Difficulty d) => d switch
    {
        Difficulty.Relaxed => new(d, "Relaxed", 0.75, 0, 0.70, 1.00, 0.60,
            "Foes are frailer, ambushes rarer and camps safer — a gentler descent."),
        Difficulty.Hard => new(d, "Hard", 1.35, 1, 1.25, 1.20, 1.30,
            "Foes are tougher and hit harder, and ambushes strike more often — but the spoils are richer."),
        _ => new(Difficulty.Normal, "Normal", 1.00, 0, 1.00, 1.00, 1.00,
            "The classic Bard's Tale balance.")
    };

    /// <summary>The baseline profile (Normal) — a convenient default for scaling.</summary>
    public static readonly DifficultyProfile Normal = For(Difficulty.Normal);
}
