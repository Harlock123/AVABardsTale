using BardsTale.Core.Town;

namespace BardsTale.Core.Quests;

/// <summary>What a side quest asks the party to do. All three tick the same way —
/// by defeating a target monster in the catacombs — but read differently.</summary>
public enum QuestKind
{
    /// <summary>Slay a number of a particular monster ("cull 6 Kobolds").</summary>
    Hunt,
    /// <summary>Gather trophies dropped by a monster ("3 Spider Venom Glands").</summary>
    Collect,
    /// <summary>Recover a single named relic carried by a tougher foe.</summary>
    Retrieve
}

/// <summary>Who handed the quest out — also decides where it is turned in.</summary>
public enum QuestGiver
{
    /// <summary>Garth, at the Equipment Shoppe. Turned in at the Shop.</summary>
    Shopkeeper,
    /// <summary>A tavern regular. Turned in at the Tavern.</summary>
    TavernPatron,
    /// <summary>Someone met on the town streets — found again in the Tavern to turn in.</summary>
    Stranger
}

public enum QuestStatus
{
    /// <summary>Accepted and in progress.</summary>
    Active,
    /// <summary>Objective met — return to the giver to collect the reward.</summary>
    ReadyToTurnIn,
    /// <summary>Reward claimed; archived.</summary>
    Completed
}

/// <summary>
/// A single side quest. Progress is just a counter incremented when the party
/// defeats <see cref="TargetMonster"/>; the flavour around it (hunt / collect /
/// retrieve) is presentation. Deliberately a plain mutable object so it round-trips
/// cleanly through the save file.
/// </summary>
public sealed class Quest
{
    /// <summary>Stable id, assigned by the <see cref="QuestLog"/> when accepted.</summary>
    public string Id { get; set; } = "";

    public QuestKind Kind { get; set; }
    public QuestGiver Giver { get; set; }

    /// <summary>Display name of whoever gave it ("Garth", "a hooded stranger").</summary>
    public string GiverName { get; set; } = "";

    /// <summary>Where the quest is handed back in — the Shop or a Tavern.</summary>
    public TownBuilding TurnInAt { get; set; }

    /// <summary>The <see cref="Combat.MonsterTemplate.Name"/> whose defeat advances this quest.</summary>
    public string TargetMonster { get; set; } = "";

    /// <summary>Noun for the thing being collected/retrieved ("Spider Venom Gland", "Silver Chalice").</summary>
    public string TrophyName { get; set; } = "";

    public int Required { get; set; } = 1;
    public int Current { get; set; }

    public int RewardGold { get; set; }
    public int RewardXp { get; set; }

    /// <summary>Optional bonus item (a known <see cref="Items.Items"/> name), granted on turn-in.</summary>
    public string? RewardItem { get; set; }

    public QuestStatus Status { get; set; } = QuestStatus.Active;

    public bool IsObjectiveMet => Current >= Required;

    /// <summary>A short headline for the quest log.</summary>
    public string Title => Kind switch
    {
        QuestKind.Hunt => $"Cull the {Plural(TargetMonster)}",
        QuestKind.Collect => $"Gather {Plural(TrophyName)}",
        QuestKind.Retrieve => $"Recover the {TrophyName}",
        _ => "A Task"
    };

    /// <summary>One-line objective with progress, e.g. "Spider Venom Glands: 2 / 3".</summary>
    public string Objective => Kind switch
    {
        QuestKind.Hunt => $"Slay {TargetMonster}: {Current} / {Required}",
        QuestKind.Collect => $"{Plural(TrophyName)} from {Plural(TargetMonster)}: {Current} / {Required}",
        QuestKind.Retrieve => $"{TrophyName} (from a {TargetMonster}): {Current} / {Required}",
        _ => $"{Current} / {Required}"
    };

    /// <summary>Where to take it once done.</summary>
    public string TurnInHint => TurnInAt == TownBuilding.Shop
        ? $"Return to {GiverName} at Garth's Equipment Shoppe."
        : $"Find {GiverName} again at a tavern.";

    /// <summary>The reward, as a readable line.</summary>
    public string RewardLine
    {
        get
        {
            var parts = new List<string>();
            if (RewardGold > 0) parts.Add($"{RewardGold} gold");
            if (RewardItem is not null) parts.Add(RewardItem);
            if (RewardXp > 0) parts.Add($"{RewardXp} XP");
            return parts.Count == 0 ? "their gratitude" : string.Join(", ", parts);
        }
    }

    /// <summary>Flavourful pitch shown when the quest is offered and in the log.</summary>
    public string Pitch => Kind switch
    {
        QuestKind.Hunt =>
            $"\"{Plural(TargetMonster)} have grown bold in the catacombs. Thin their numbers — "
            + $"{Required} of them — and I'll make it worth your while.\"",
        QuestKind.Collect =>
            $"\"Bring me {Required} {Plural(TrophyName)} from the {Plural(TargetMonster)} below. "
            + "I've a buyer who pays handsomely for such things.\"",
        QuestKind.Retrieve =>
            $"\"A {TargetMonster} in the depths carries the {TrophyName} — it was stolen from me. "
            + "Recover it and the reward is yours.\"",
        _ => "\"I have a task for you.\""
    };

    private static string Plural(string noun)
    {
        if (string.IsNullOrEmpty(noun)) return noun;
        if (noun.EndsWith('s') || noun.EndsWith('x') || noun.EndsWith("ch")) return noun + "es";
        if (noun.EndsWith('y') && noun.Length > 1 && !"aeiou".Contains(noun[^2]))
            return noun[..^1] + "ies";
        return noun + "s";
    }
}
