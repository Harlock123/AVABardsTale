using BardsTale.Core.Characters;
using BardsTale.Core.Combat;

namespace BardsTale.Core.Items;

/// <summary>
/// A bonus granted for wearing a matched pair of accessories together — a classic
/// "set bonus". Pieces are matched by base name (a "+2" enchant still counts), and
/// duplicate pieces in the list require duplicate slots (e.g. two Rings of Protection).
/// </summary>
public sealed record AccessorySet(
    string Name,
    string Description,
    IReadOnlyList<string> Pieces,
    int ArmorBonus = 0,
    Element WardBonus = Element.None,
    int RegenBonus = 0,
    StatusEffect ImmuneBonus = StatusEffect.None,
    int HitBonus = 0,
    int DamageBonus = 0);

/// <summary>The catalogue of accessory set bonuses and the rules for which are active.</summary>
public static class AccessorySets
{
    public static readonly IReadOnlyList<AccessorySet> All = new[]
    {
        new AccessorySet("Twin Bulwark",
            "Two Rings of Protection — an extra point of armour.",
            new[] { "Ring of Protection", "Ring of Protection" }, ArmorBonus: 1),
        new AccessorySet("Stormwarden",
            "Ring of Storm Ward + Amulet of Warding — also wards arcane.",
            new[] { "Ring of Storm Ward", "Amulet of Warding" }, WardBonus: Element.Arcane),
        new AccessorySet("Vital Coil",
            "Ring of Regeneration + Amulet of the Viper — quicker mending and poison immunity.",
            new[] { "Ring of Regeneration", "Amulet of the Viper" }, RegenBonus: 1, ImmuneBonus: StatusEffect.Poisoned),
        new AccessorySet("Duelist's Edge",
            "Ring of Striking + Amulet of Valor — a sharper, surer strike.",
            new[] { "Ring of Striking", "Amulet of Valor" }, HitBonus: 1, DamageBonus: 1),
        new AccessorySet("Berserker's Fury",
            "Ring of Striking + Ring of Accuracy — a relentless, accurate offence.",
            new[] { "Ring of Striking", "Ring of Accuracy" }, HitBonus: 1, DamageBonus: 1),
        new AccessorySet("Warden's Resolve",
            "Ring of Free Action + Amulet of the Viper — unshakeable against every affliction.",
            new[] { "Ring of Free Action", "Amulet of the Viper" }, ImmuneBonus: StatusEffect.Poisoned),
        // A three-piece set — both rings and the amulet — for those who go all-in on warding.
        new AccessorySet("Elementalist's Regalia",
            "Ring of Fire Ward + Ring of Frost Ward + Amulet of the Viper — completes the elements.",
            new[] { "Ring of Fire Ward", "Ring of Frost Ward", "Amulet of the Viper" },
            ArmorBonus: 1, WardBonus: Element.Lightning | Element.Arcane),
    };

    /// <summary>The sets whose every piece the character currently wears (base names, "+N" enchants count).</summary>
    public static IEnumerable<AccessorySet> ActiveFor(Character c)
    {
        var worn = c.Accessories.Select(a => Items.BaseName(a.Name)).ToList();
        foreach (var set in All)
            if (Missing(worn, set.Pieces).Count == 0)
                yield return set;
    }

    /// <summary>
    /// Nudges for sets the character is exactly one piece away from completing, e.g.
    /// "Equip an Amulet of Warding to complete Stormwarden." — to surface set bonuses.
    /// </summary>
    public static IEnumerable<string> HintsFor(Character c)
    {
        var worn = c.Accessories.Select(a => Items.BaseName(a.Name)).ToList();
        foreach (var set in All)
        {
            var missing = Missing(worn, set.Pieces);
            if (missing.Count != 1) continue;
            var piece = missing[0];
            var article = worn.Contains(piece) ? "another" : StartsWithVowel(piece) ? "an" : "a";
            yield return $"Equip {article} {piece} to complete {set.Name}.";
        }
    }

    // Certain lair bosses guard a complete set as a guaranteed reward.
    private static readonly Dictionary<string, string> BossRewards = new()
    {
        ["Stone Titan"] = "Twin Bulwark",
        ["Vampire Lord"] = "Vital Coil",
        ["Beholder Tyrant"] = "Stormwarden",
        ["Death Tyrant"] = "Warden's Resolve",
        ["Pit Lord"] = "Berserker's Fury",
        ["Dragon Tyrant"] = "Elementalist's Regalia",
    };

    /// <summary>The set a given boss is guaranteed to drop, or null if it guards no set.</summary>
    public static AccessorySet? RewardForBoss(string bossName) =>
        BossRewards.TryGetValue(bossName, out var setName) ? All.First(s => s.Name == setName) : null;

    /// <summary>The (identified) item pieces that make up a set — for boss/themed drops.</summary>
    public static IReadOnlyList<Item> PiecesOf(AccessorySet set) =>
        set.Pieces.Select(p => Items.Find(p)!).ToList();

    /// <summary>Every set fully present (by base name) in a haul — for "you found the X set!" callouts.</summary>
    public static IEnumerable<AccessorySet> SetsIn(IEnumerable<Item> items)
    {
        var names = items.Where(i => i.IsAccessory).Select(i => Items.BaseName(i.Name)).ToList();
        foreach (var set in All)
            if (Missing(names, set.Pieces).Count == 0)
                yield return set;
    }

    // The set pieces not yet matched by a distinct worn slot (multiset difference).
    private static List<string> Missing(List<string> worn, IReadOnlyList<string> required)
    {
        var pool = new List<string>(worn);
        var missing = new List<string>();
        foreach (var piece in required)
            if (!pool.Remove(piece))
                missing.Add(piece);
        return missing;
    }

    private static bool StartsWithVowel(string s) => s.Length > 0 && "AEIOUaeiou".IndexOf(s[0]) >= 0;
}
