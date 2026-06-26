namespace BardsTale.Core.Items;

/// <summary>
/// Builds Garth's Equipment Shoppe stock from the party's progress. The base wares (weapons,
/// armour, potions and entry-tier wards) are always on the shelves; Garth begins stocking
/// progressively stronger effect and vitality accessories as the party proves it can reach
/// deeper floors — the middle ground between "buy everything" and "treasure-only".
/// </summary>
public static class ShopWares
{
    private static readonly Item[] Base =
    {
        Items.Dagger, Items.ShortSword, Items.LongSword, Items.BattleAxe, Items.Staff,
        // Ranged weapons — arm the back rank so it can fight past the front line.
        Items.Sling, Items.ShortBow, Items.Crossbow,
        Items.LeatherArmor, Items.ChainMail, Items.PlateMail, Items.SmallShield, Items.Robes,
        // Entry-tier defensive accessories — a starter ward kit.
        Items.RingOfProtection, Items.RingOfFireWard, Items.RingOfFrostWard,
        Items.RingOfStormWard, Items.AmuletOfTheViper,
        Items.HealingPotion, Items.ManaDraught, Items.Antidote, Items.ResurrectionDust
    };

    // Accessories Garth starts stocking once the party has reached a given dungeon floor.
    // (The legendary Talisman of the Ages stays treasure-only.)
    private static readonly (int Depth, Item[] Wares)[] Unlocks =
    {
        (4,  new[] { Items.RingOfAccuracy, Items.RingOfStriking, Items.AmuletOfFortune }),
        (8,  new[] { Items.RingOfRegeneration, Items.AmuletOfValor, Items.RingOfVigor }),
        (12, new[] { Items.AmuletOfWarding, Items.RingOfFreeAction }),
        (16, new[] { Items.AmuletOfTheMagi, Items.AmuletOfVitality }),
    };

    /// <summary>Garth's stock given the deepest dungeon floor the party has reached.</summary>
    public static IReadOnlyList<Item> For(int deepestDepth)
    {
        var stock = new List<Item>(Base);
        foreach (var (depth, wares) in Unlocks)
            if (deepestDepth >= depth)
                stock.AddRange(wares);
        return stock;
    }

    /// <summary>The next floor at which Garth's stock expands, or null once everything's unlocked.</summary>
    public static int? NextUnlockDepth(int deepestDepth)
    {
        foreach (var (depth, _) in Unlocks)
            if (deepestDepth < depth)
                return depth;
        return null;
    }

    /// <summary>The unlock floors newly crossed by descending from one deepest depth to another.</summary>
    public static IReadOnlyList<int> NewUnlocksBetween(int previousDeepest, int currentDeepest)
    {
        var crossed = new List<int>();
        foreach (var (depth, _) in Unlocks)
            if (depth > previousDeepest && depth <= currentDeepest)
                crossed.Add(depth);
        return crossed;
    }
}
