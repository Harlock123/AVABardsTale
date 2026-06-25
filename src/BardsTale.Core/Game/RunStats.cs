namespace BardsTale.Core.Game;

/// <summary>Tallies the party's deeds across a playthrough, for the victory/credits screen.</summary>
public sealed class RunStats
{
    public int BattlesWon { get; set; }
    public int MonstersSlain { get; set; }
    public int GoldEarned { get; set; }
    public int DeepestDepth { get; set; } = 1;

    /// <summary>True once the party has defeated Mangar and won the game.</summary>
    public bool Victory { get; set; }
}
