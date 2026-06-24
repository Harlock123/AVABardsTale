using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Town;
using ItemDb = BardsTale.Core.Items.Items;

namespace BardsTale.Core.Quests;

/// <summary>
/// The party's side-quest journal: the quests currently being worked (including
/// those ready to hand in) and an archive of finished ones. Progress is driven by
/// <see cref="RecordVictory"/>, called after each won fight, and rewards are paid
/// out by <see cref="Claim"/> when the party returns to the giver.
/// </summary>
public sealed class QuestLog
{
    /// <summary>How many quests can be on the go at once.</summary>
    public const int MaxActive = 5;

    private readonly List<Quest> _active = new();
    private readonly List<Quest> _completed = new();
    private int _nextId = 1;

    /// <summary>Quests in progress or ready to turn in.</summary>
    public IReadOnlyList<Quest> Active => _active;

    /// <summary>Quests whose rewards have been claimed.</summary>
    public IReadOnlyList<Quest> Completed => _completed;

    public bool IsFull => _active.Count >= MaxActive;
    public int ActiveCount => _active.Count;
    public bool HasAny => _active.Count > 0;

    /// <summary>True if a quest from this giver is already on the go (avoids piling up duplicates).</summary>
    public bool HasActiveFrom(QuestGiver giver) => _active.Any(q => q.Giver == giver);

    /// <summary>Accepts a freshly offered quest, assigning it a stable id.</summary>
    public bool Accept(Quest quest)
    {
        if (IsFull) return false;
        if (string.IsNullOrEmpty(quest.Id)) quest.Id = $"q{_nextId++}";
        quest.Status = quest.IsObjectiveMet ? QuestStatus.ReadyToTurnIn : QuestStatus.Active;
        _active.Add(quest);
        return true;
    }

    /// <summary>
    /// Advances every active quest by the monsters slain in <paramref name="encounter"/>.
    /// Returns log lines describing any progress or newly-completable quests.
    /// </summary>
    public IReadOnlyList<string> RecordVictory(Encounter encounter)
    {
        var messages = new List<string>();
        foreach (var quest in _active)
        {
            if (quest.Status != QuestStatus.Active) continue;
            var slain = encounter.Groups
                .Where(g => g.Template.Name == quest.TargetMonster)
                .Sum(g => g.Monsters.Count);
            if (slain == 0) continue;

            quest.Current = Math.Min(quest.Required, quest.Current + slain);
            if (quest.IsObjectiveMet)
            {
                quest.Status = QuestStatus.ReadyToTurnIn;
                messages.Add($"Quest complete: \"{quest.Title}\" — {quest.TurnInHint}");
            }
            else
            {
                messages.Add($"Quest \"{quest.Title}\": {quest.Current} / {quest.Required}.");
            }
        }
        return messages;
    }

    /// <summary>Drops an active quest without reward — its progress is lost. Returns true if it was found.</summary>
    public bool Abandon(Quest quest) => _active.Remove(quest);

    /// <summary>Quests ready to be handed in at the given building.</summary>
    public IReadOnlyList<Quest> ReadyAt(TownBuilding building) =>
        _active.Where(q => q.Status == QuestStatus.ReadyToTurnIn && q.TurnInAt == building).ToList();

    /// <summary>
    /// Pays out a completed quest's reward into the party and archives it.
    /// Returns a sentence describing the reward (or null if the quest wasn't claimable).
    /// </summary>
    public string? Claim(Quest quest, Party party)
    {
        if (quest.Status != QuestStatus.ReadyToTurnIn || !_active.Contains(quest)) return null;

        party.Gold += quest.RewardGold;

        var living = party.Members.Where(m => !m.IsDead).ToList();
        if (quest.RewardXp > 0 && living.Count > 0)
        {
            var each = quest.RewardXp / living.Count;
            foreach (var member in living)
                member.Experience += each;
        }

        var itemNote = "";
        if (quest.RewardItem is not null && ItemDb.Find(quest.RewardItem) is { } item)
        {
            party.Inventory.Add(item);
            itemNote = $" and a {item.Name}";
        }

        quest.Status = QuestStatus.Completed;
        _active.Remove(quest);
        _completed.Add(quest);

        var xpNote = quest.RewardXp > 0 ? $" (+{quest.RewardXp} XP)" : "";
        return $"{quest.GiverName} hands over {quest.RewardGold} gold{itemNote}{xpNote}.";
    }

    // --- save / load support ---

    /// <summary>Restores a quest straight into the active list (used by the loader).</summary>
    public void RestoreActive(Quest quest) => _active.Add(quest);

    /// <summary>Restores an archived quest (used by the loader).</summary>
    public void RestoreCompleted(Quest quest) => _completed.Add(quest);

    /// <summary>The next id to assign — persisted so ids stay unique across a save/load.</summary>
    public int NextId
    {
        get => _nextId;
        set => _nextId = value;
    }
}
