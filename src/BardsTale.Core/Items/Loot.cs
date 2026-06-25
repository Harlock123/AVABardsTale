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
            // A warding ring or amulet occasionally turns up deeper down.
            if (depth >= 3 && rng.Chance(0.03 + 0.004 * depth)) drops.Add(RollAccessory(rng, depth));
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

            // A handful of lair bosses guard a complete accessory set as a guaranteed reward.
            var bossName = encounter.Groups[0].Template.Name;
            if (AccessorySets.RewardForBoss(bossName) is { } set)
                drops.AddRange(AccessorySets.PiecesOf(set));
        }

        return drops;
    }

    /// <summary>
    /// The spoils of a treasure chest: a purse of gold scaled to the depth, plus one to three
    /// items biased toward warding accessories and enchanted gear — richer than a wandering drop.
    /// </summary>
    public static (int Gold, List<Item> Items) RollChest(IRandomSource rng, int depth, bool ornate = false)
    {
        var gold = rng.Roll(2, 20, 10) * (3 + depth);
        if (ornate) gold = gold * 3 / 2 + depth * 25; // a gilded chest holds a fatter purse
        var items = new List<Item>();

        // An ornate chest always yields an accessory: sometimes a whole matched set
        // (a themed drop the party can equip together), otherwise a single warding piece.
        if (ornate)
        {
            if (rng.Chance(0.4)) items.AddRange(SetDrop(rng));
            else items.Add(RollAccessory(rng, depth).AsUnidentified());
        }

        // A chest always holds at least one prize; deeper and ornate chests hold more.
        var prizes = 1 + (rng.Chance(0.45) ? 1 : 0) + (depth >= 8 && rng.Chance(0.35) ? 1 : 0) + (ornate ? 1 : 0);
        for (var i = 0; i < prizes; i++)
        {
            var roll = rng.Next(0, 100);
            if (roll < 35) items.Add(RollAccessory(rng, depth).AsUnidentified());
            else if (roll < 60) items.Add(RollMagic(rng));
            else if (roll < 75 && depth >= 6) items.Add(rng.Pick(Items.PowerItems).AsUnidentified());
            else if (roll < 90) items.Add(rng.Pick(PotionDrops));
            else items.Add(Items.ResurrectionDust);
        }
        // Forge embers sweeten a deep chest.
        if (depth >= 4 && rng.Chance(ornate ? 0.8 : 0.5)) items.Add(Items.ForgeEmber);
        return (gold, items);
    }

    /// <summary>The matched pieces of a random accessory set — a themed haul, identified so the set reads at a glance.</summary>
    private static IEnumerable<Item> SetDrop(IRandomSource rng)
    {
        var set = rng.Pick(AccessorySets.All);
        return set.Pieces.Select(p => Items.Find(p)!);
    }

    /// <summary>Picks a warding accessory, with the most potent talismans reserved for the deep.</summary>
    private static Item RollAccessory(IRandomSource rng, int depth)
    {
        var pool = Items.Accessories
            .Where(a => a.Value <= 600 + depth * 250)
            .DefaultIfEmpty(Items.RingOfProtection)
            .ToList();
        return rng.Pick(pool);
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
