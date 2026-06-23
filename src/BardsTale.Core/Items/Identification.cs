namespace BardsTale.Core.Items;

/// <summary>Rules for appraising unidentified items by a Rogue's eye.</summary>
public static class Identification
{
    /// <summary>A Rogue's odds of correctly appraising an item, improving with level.</summary>
    public static double RogueChance(int level) => Math.Min(0.95, 0.35 + 0.08 * level);
}
