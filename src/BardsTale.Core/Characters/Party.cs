using BardsTale.Core.Geometry;
using BardsTale.Core.Items;

namespace BardsTale.Core.Characters;

/// <summary>
/// The player's adventuring party: up to six members plus their shared position
/// and facing within the current maze level.
/// </summary>
public sealed class Party
{
    public const int MaxSize = 6;

    private readonly List<Character> _members = new();

    public IReadOnlyList<Character> Members => _members;

    public Position Position { get; set; }
    public Direction Facing { get; set; } = Direction.North;

    /// <summary>The spendable purse — what cutpurses can steal from.</summary>
    public int Gold { get; set; }

    /// <summary>Gold deposited at the Bank: out of the purse, and safe from theft.</summary>
    public int BankedGold { get; set; }

    /// <summary>Shared loot stash: consumables and unequipped gear the party carries.</summary>
    public List<Item> Inventory { get; } = new();

    public IEnumerable<Item> Consumables => Inventory.Where(i => i.IsConsumable);

    public bool IsWiped => _members.Count > 0 && _members.All(m => m.IsDead);
    public int LivingCount => _members.Count(m => !m.IsDead);

    /// <summary>Front rank (first three living members) can make melee attacks.</summary>
    public IEnumerable<Character> FrontRank => _members.Where(m => !m.IsDead).Take(3);

    public bool Add(Character c)
    {
        if (_members.Count >= MaxSize) return false;
        _members.Add(c);
        return true;
    }

    public bool Remove(Character c) => _members.Remove(c);

    public Character? this[int index] =>
        index >= 0 && index < _members.Count ? _members[index] : null;

    /// <summary>Rests the party: fully heals and cures all living members. Used at safe locations.</summary>
    public void Rest()
    {
        foreach (var m in _members)
        {
            m.CureAilments();
            m.FullHeal();
        }
    }
}
