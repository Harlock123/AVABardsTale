using BardsTale.Core.Util;

namespace BardsTale.Core.Quests;

/// <summary>
/// The town notice board: a small rotating set of quests the party can browse and
/// pick up at will, rather than waiting for a giver to offer one. Postings are
/// transient — a fresh batch is pinned up after each trip into the catacombs — so
/// the board isn't part of the save file.
/// </summary>
public sealed class QuestBoard
{
    /// <summary>How many notices are pinned up at once.</summary>
    public const int Capacity = 4;

    private readonly List<Quest> _postings = new();
    private bool _stocked;

    public IReadOnlyList<Quest> Postings => _postings;
    public bool HasPostings => _postings.Count > 0;

    /// <summary>Pins up the first batch of notices; a no-op once the board has been stocked.</summary>
    public void EnsureStocked(IRandomSource rng, int depth)
    {
        if (_stocked) return;
        Restock(rng, depth);
    }

    /// <summary>Replaces every notice with a fresh batch (called when the party returns to town).</summary>
    public void Restock(IRandomSource rng, int depth)
    {
        _postings.Clear();
        var givers = new[] { QuestGiver.Shopkeeper, QuestGiver.TavernPatron, QuestGiver.Stranger };
        for (var i = 0; i < Capacity; i++)
            _postings.Add(QuestFactory.Create(rng.Pick(givers), rng, Math.Max(1, depth)));
        _stocked = true;
    }

    /// <summary>Takes a notice down (when accepted into the journal).</summary>
    public void Remove(Quest quest) => _postings.Remove(quest);
}
