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

    public static List<Item> Roll(Encounter encounter, IRandomSource rng, int depth = 1)
    {
        var drops = new List<Item>();
        foreach (var _ in encounter.Groups)
        {
            if (rng.Chance(0.35)) drops.Add(rng.Pick(PotionDrops));
            if (rng.Chance(0.08)) drops.Add(rng.Pick(EquipmentDrops));
            if (rng.Chance(0.03)) drops.Add(Items.ResurrectionDust);
            if (rng.Chance(0.07)) drops.Add(RollMagic(rng));
            // Forge embers (Smithy fuel) turn up on the deeper floors.
            if (depth >= 4 && rng.Chance(0.10 + 0.01 * depth)) drops.Add(Items.ForgeEmber);
            // A powered wand or staff is a rare deep find.
            if (depth >= 6 && rng.Chance(0.02 + 0.004 * depth)) drops.Add(rng.Pick(Items.PowerItems).AsUnidentified());
        }

        // Bosses always yield treasure; Mangar yields his signature staff.
        if (encounter.IsFinalBoss)
        {
            drops.Add(Items.MangarsStaff);
            for (var i = 0; i < 3; i++) drops.Add(Items.ForgeEmber);
        }
        else if (encounter.IsBoss)
        {
            drops.Add(RollMagic(rng));
            for (var i = 0; i < 1 + depth / 5; i++) drops.Add(Items.ForgeEmber);
            if (depth >= 5 && rng.Chance(0.5)) drops.Add(rng.Pick(Items.PowerItems).AsUnidentified());
        }

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
