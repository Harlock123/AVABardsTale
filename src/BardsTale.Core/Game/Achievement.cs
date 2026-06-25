namespace BardsTale.Core.Game;

/// <summary>A snapshot of the run's tracked deeds, evaluated against the achievement list.</summary>
public sealed record AchievementContext(
    int DeepestDepth,
    int BattlesWon,
    int MonstersSlain,
    int GoldEarned,
    int BestiaryDiscovered,
    int QuestsCompleted,
    bool Victorious);

/// <summary>One earnable achievement and the renown it grants when unlocked.</summary>
public sealed record Achievement(string Id, string Name, string Description, int Renown,
    Func<AchievementContext, bool> Met);

/// <summary>The full list of achievements, checked against the run as the party plays.</summary>
public static class Achievements
{
    public static readonly IReadOnlyList<Achievement> All = new[]
    {
        // --- Plumbing the depths ---
        new Achievement("DEPTH2",  "First Steps",           "Descend to dungeon level 2.",            5,  c => c.DeepestDepth >= 2),
        new Achievement("DEPTH5",  "Into the Deep",         "Descend to level 5.",                   10,  c => c.DeepestDepth >= 5),
        new Achievement("DEPTH10", "Halfway Down",          "Descend to level 10.",                  20,  c => c.DeepestDepth >= 10),
        new Achievement("DEPTH15", "The Lower Dark",        "Descend to level 15.",                  30,  c => c.DeepestDepth >= 15),
        new Achievement("DEPTH20", "Bottom of the World",   "Stand on the deepest level, 20.",       50,  c => c.DeepestDepth >= 20),

        // --- Battle ---
        new Achievement("WIN10",   "Blooded",               "Win 10 battles.",                       10,  c => c.BattlesWon >= 10),
        new Achievement("WIN50",   "Veteran",               "Win 50 battles.",                       25,  c => c.BattlesWon >= 50),
        new Achievement("SLAY100", "Monster Hunter",        "Slay 100 monsters.",                    20,  c => c.MonstersSlain >= 100),
        new Achievement("SLAY500", "Exterminator",          "Slay 500 monsters.",                    40,  c => c.MonstersSlain >= 500),

        // --- The bestiary ---
        new Achievement("BEST30",  "Naturalist",            "Record 30 monsters in the bestiary.",   15,  c => c.BestiaryDiscovered >= 30),
        new Achievement("BEST60",  "Loremaster",            "Record 60 monsters.",                   30,  c => c.BestiaryDiscovered >= 60),
        new Achievement("BEST120", "The Complete Bestiary", "Record all 120 monsters.",              75,  c => c.BestiaryDiscovered >= 120),

        // --- Side quests ---
        new Achievement("QUEST5",  "Errand Runner",         "Complete 5 side quests.",               15,  c => c.QuestsCompleted >= 5),
        new Achievement("QUEST20", "Hero for Hire",         "Complete 20 side quests.",              35,  c => c.QuestsCompleted >= 20),

        // --- Wealth ---
        new Achievement("GOLD5K",  "Treasure Hunter",       "Plunder 5,000 gold from foes.",         15,  c => c.GoldEarned >= 5000),
        new Achievement("GOLD50K", "Dragon's Hoard",        "Plunder 50,000 gold from foes.",        40,  c => c.GoldEarned >= 50000),

        // --- The win ---
        new Achievement("MANGAR",  "Liberator of Skara Brae","Destroy Mangar the Mad.",             100,  c => c.Victorious),
    };

    private static readonly Dictionary<string, Achievement> ById = All.ToDictionary(a => a.Id);

    public static Achievement Get(string id) => ById[id];
    public static bool Exists(string id) => ById.ContainsKey(id);

    /// <summary>Renown if every achievement were earned — the denominator for completion.</summary>
    public static int TotalRenown => All.Sum(a => a.Renown);
}
