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

    /// <summary>
    /// A single number summarising the run, used to rank seeded daily challenges. Depth reached
    /// dominates, with bonuses for battles won, monsters slain, gold plundered, and a big payout
    /// for finishing the game.
    /// </summary>
    public int Score
    {
        get
        {
            var score = (DeepestDepth - 1) * 1000
                        + BattlesWon * 25
                        + MonstersSlain * 10
                        + GoldEarned / 10;
            if (Victory) score += 10000;
            return score;
        }
    }
}
