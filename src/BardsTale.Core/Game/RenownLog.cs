namespace BardsTale.Core.Game;

/// <summary>
/// The party's standing: which achievements they've unlocked, the renown that earns
/// them, and the town discount that renown buys. Persisted with the save.
/// </summary>
public sealed class RenownLog
{
    private readonly HashSet<string> _unlocked = new();

    public IReadOnlyCollection<string> UnlockedIds => _unlocked;
    public bool IsUnlocked(string id) => _unlocked.Contains(id);
    public int UnlockedCount => _unlocked.Count;

    /// <summary>Total renown — the sum of every unlocked achievement's value.</summary>
    public int Renown => Achievements.All.Where(a => _unlocked.Contains(a.Id)).Sum(a => a.Renown);

    /// <summary>
    /// Renown buys goodwill in town — a discount on the smith, trainer, temple healing
    /// and the inn, climbing to a 25% cap at 250 renown.
    /// </summary>
    public double Discount => Math.Min(0.25, Renown / 1000.0);

    /// <summary>
    /// Unlocks any achievement whose condition is now met, and returns just the ones
    /// newly earned this call (so the caller can announce them).
    /// </summary>
    public IReadOnlyList<Achievement> Sync(RunStats stats, int bestiaryDiscovered, int questsCompleted)
    {
        var ctx = new AchievementContext(stats.DeepestDepth, stats.BattlesWon, stats.MonstersSlain,
            stats.GoldEarned, bestiaryDiscovered, questsCompleted, stats.Victory);

        var newly = new List<Achievement>();
        foreach (var achievement in Achievements.All)
            if (!_unlocked.Contains(achievement.Id) && achievement.Met(ctx))
            {
                _unlocked.Add(achievement.Id);
                newly.Add(achievement);
            }
        return newly;
    }

    /// <summary>Reinstates an unlocked achievement from a saved game.</summary>
    public void Restore(string id)
    {
        if (Achievements.Exists(id)) _unlocked.Add(id);
    }
}
