using BardsTale.Core.Geometry;
using BardsTale.Core.Items;

namespace BardsTale.Core.Characters;

/// <summary>A shrine-granted, dive-long blessing on the whole party — a "player-side affix".</summary>
public enum PartyBoon
{
    None,
    /// <summary>The party strikes harder.</summary>
    Might,
    /// <summary>The party is harder to hit.</summary>
    Warding,
    /// <summary>The party mends a little each round of battle.</summary>
    Vigor
}

/// <summary>Magnitudes and labels for the party boons (see <see cref="PartyBoon"/>), shared by the
/// combat engine (which applies them) and the shrine narration (which describes them).</summary>
public static class Boons
{
    public const int MightDamage = 2;
    public const int WardingArmor = 2;
    public const int VigorRegen = 3;

    public static string Label(PartyBoon b) => b switch
    {
        PartyBoon.Might => "Might",
        PartyBoon.Warding => "Warding",
        PartyBoon.Vigor => "Vigor",
        _ => ""
    };

    public static string Describe(PartyBoon b) => b switch
    {
        PartyBoon.Might => $"the party strikes {MightDamage} harder",
        PartyBoon.Warding => "the party is harder to strike",
        PartyBoon.Vigor => $"the party mends {VigorRegen} HP each round of battle",
        _ => ""
    };
}

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

    /// <summary>Iron keys the party carries, spent one at a time to open locked dungeon doors.</summary>
    public int Keys { get; set; }

    /// <summary>A shrine-granted blessing in effect for the current dive (cleared on returning to town).</summary>
    public PartyBoon Boon { get; set; } = PartyBoon.None;

    /// <summary>Shared loot stash: consumables and unequipped gear the party carries.</summary>
    public List<Item> Inventory { get; } = new();

    public IEnumerable<Item> Consumables => Inventory.Where(i => i.IsConsumable);

    public bool IsWiped => _members.Count > 0 && _members.All(m => m.IsDead);
    public int LivingCount => _members.Count(m => !m.IsDead);

    /// <summary>Front rank (first three living members) can make melee attacks.</summary>
    public IEnumerable<Character> FrontRank => _members.Where(m => !m.IsDead).Take(3);

    /// <summary>True when this living member stands in the front rank (and so can melee).</summary>
    public bool IsInFrontRank(Character c) => FrontRank.Contains(c);

    /// <summary>
    /// Swaps a member with the one above it in the marching order — the way the player arranges
    /// who stands in the front rank (melee &amp; the monsters' targets) versus the safer back rank.
    /// </summary>
    public bool MoveUp(Character c)
    {
        var i = _members.IndexOf(c);
        if (i <= 0) return false;
        (_members[i - 1], _members[i]) = (_members[i], _members[i - 1]);
        return true;
    }

    /// <summary>Swaps a member with the one below it in the marching order.</summary>
    public bool MoveDown(Character c)
    {
        var i = _members.IndexOf(c);
        if (i < 0 || i >= _members.Count - 1) return false;
        (_members[i + 1], _members[i]) = (_members[i], _members[i + 1]);
        return true;
    }

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
            m.RefreshBardTunes();
        }
    }
}
