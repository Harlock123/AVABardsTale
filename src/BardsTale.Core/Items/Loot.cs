using BardsTale.Core.Combat;
using BardsTale.Core.Util;

namespace BardsTale.Core.Items;

/// <summary>Rolls the spoils dropped by a defeated encounter.</summary>
public static class Loot
{
    private static readonly IReadOnlyList<Item> PotionDrops = new[]
    {
        Items.HealingPotion, Items.HealingPotion, Items.ManaDraught, Items.Antidote
    };

    private static readonly IReadOnlyList<Item> EquipmentDrops = new[]
    {
        Items.ShortSword, Items.LongSword, Items.LeatherArmor, Items.ChainMail, Items.SmallShield
    };

    public static List<Item> Roll(Encounter encounter, IRandomSource rng)
    {
        var drops = new List<Item>();
        foreach (var _ in encounter.Groups)
        {
            if (rng.Chance(0.35)) drops.Add(rng.Pick(PotionDrops));
            if (rng.Chance(0.08)) drops.Add(rng.Pick(EquipmentDrops));
            if (rng.Chance(0.03)) drops.Add(Items.ResurrectionDust);
            if (rng.Chance(0.07)) drops.Add(RollMagic(rng));
        }

        // Bosses always yield treasure; Mangar yields his signature staff.
        if (encounter.IsFinalBoss)
            drops.Add(Items.MangarsStaff);
        else if (encounter.IsBoss)
            drops.Add(RollMagic(rng));

        return drops;
    }

    /// <summary>Picks a magic item, favouring lower enchantments (+1 common, +3 rare).</summary>
    private static Item RollMagic(IRandomSource rng)
    {
        var roll = rng.Next(0, 100);
        var bonus = roll < 60 ? 1 : roll < 90 ? 2 : 3;
        var candidates = Items.MagicItems.Where(i => i.MagicBonus == bonus).ToList();
        return rng.Pick(candidates).AsUnidentified(); // its bonus is a mystery until appraised
    }
}
