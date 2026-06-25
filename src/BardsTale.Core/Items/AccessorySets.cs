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
    };

    /// <summary>The sets whose every piece the character currently wears (base names, "+N" enchants count).</summary>
    public static IEnumerable<AccessorySet> ActiveFor(Character c)
    {
        var worn = c.Accessories.Select(a => Items.BaseName(a.Name)).ToList();
        foreach (var set in All)
            if (Covers(worn, set.Pieces))
                yield return set;
    }

    // Treats the required pieces as a multiset — each must be matched by a distinct worn slot.
    private static bool Covers(List<string> worn, IReadOnlyList<string> required)
    {
        var pool = new List<string>(worn);
        foreach (var piece in required)
            if (!pool.Remove(piece))
                return false;
        return true;
    }
}
