using System;

namespace BardsTale.Core.Game;

/// <summary>
/// Seeded daily challenges: a single shared seed per calendar day produces an identical run for
/// everyone — the same dungeon, monsters and loot — so scores can be compared. A challenge run
/// starts a fresh <see cref="GameSession"/> on the seed with the ready-made party, played to the
/// death (Ironman) and scored by <see cref="RunStats.Score"/>.
/// </summary>
public static class Challenges
{
    /// <summary>The seed for a given day — the same the world over, derived from the calendar date.</summary>
    public static int DailySeed(DateOnly date) => date.Year * 10000 + date.Month * 100 + date.Day;

    /// <summary>Whether a seed string entered by the player is a usable challenge seed.</summary>
    public static bool TryParseSeed(string? text, out int seed)
        => int.TryParse(text?.Trim(), out seed);
}
