namespace BardsTale.Core.Items;

public enum ItemSlot
{
    None,
    Weapon,
    Armor,
    Shield,
    Consumable
}

/// <summary>What a consumable does when used (always on a single ally).</summary>
public enum ConsumableEffect
{
    None,
    Heal,
    RestoreSpellPoints,
    Cure,
    Revive
}

/// <summary>A piece of equipment or a consumable the party can carry.</summary>
public sealed record Item(
    string Name,
    ItemSlot Slot,
    int DamageDice = 0,
    int DamageSides = 0,
    int DamageBonus = 0,
    int ArmorBonus = 0,
    int Value = 0,
    ConsumableEffect Consumable = ConsumableEffect.None,
    int Power = 0,
    int MagicBonus = 0,
    bool Identified = true)
{
    public bool IsWeapon => Slot == ItemSlot.Weapon;
    public bool IsConsumable => Slot == ItemSlot.Consumable;
    public bool IsMagic => MagicBonus > 0;

    /// <summary>What the party sees: the true name once identified, a vague label until then.</summary>
    public string DisplayName => Identified ? Name : $"Unidentified {Category}";

    private string Category => Slot switch
    {
        ItemSlot.Weapon => "Weapon",
        ItemSlot.Armor => "Armor",
        ItemSlot.Shield => "Shield",
        _ => "Item"
    };

    /// <summary>A concealed copy of this item — same stats underneath, but its identity is hidden.</summary>
    public Item AsUnidentified() => this with { Identified = false };

    /// <summary>Reveals a concealed item's true identity.</summary>
    public Item Identify() => this with { Identified = true };

    public string DamageText => DamageDice > 0
        ? $"{DamageDice}d{DamageSides}{(DamageBonus != 0 ? (DamageBonus > 0 ? "+" : "") + DamageBonus : "")}"
        : "—";

    public string EffectText => Consumable switch
    {
        ConsumableEffect.Heal => $"heal ~{Power} HP",
        ConsumableEffect.RestoreSpellPoints => $"restore ~{Power} SP",
        ConsumableEffect.Cure => "cure ailments",
        ConsumableEffect.Revive => "revive an ally",
        _ => ""
    };
}

/// <summary>A small catalogue of starting equipment for the vertical slice.</summary>
public static class Items
{
    public static readonly Item Fists = new("Fists", ItemSlot.Weapon, 1, 4);
    public static readonly Item Dagger = new("Dagger", ItemSlot.Weapon, 1, 4, 0, 0, 20);
    public static readonly Item ShortSword = new("Short Sword", ItemSlot.Weapon, 1, 6, 0, 0, 60);
    public static readonly Item LongSword = new("Long Sword", ItemSlot.Weapon, 1, 8, 0, 0, 120);
    public static readonly Item BattleAxe = new("Battle Axe", ItemSlot.Weapon, 2, 4, 0, 0, 150);
    public static readonly Item Staff = new("Quarterstaff", ItemSlot.Weapon, 1, 6, 0, 0, 30);

    public static readonly Item Robes = new("Robes", ItemSlot.Armor, ArmorBonus: 0, Value: 5);
    public static readonly Item LeatherArmor = new("Leather Armor", ItemSlot.Armor, ArmorBonus: 2, Value: 40);
    public static readonly Item ChainMail = new("Chain Mail", ItemSlot.Armor, ArmorBonus: 4, Value: 200);
    public static readonly Item PlateMail = new("Plate Mail", ItemSlot.Armor, ArmorBonus: 6, Value: 600);

    public static readonly Item SmallShield = new("Small Shield", ItemSlot.Shield, ArmorBonus: 1, Value: 30);

    // --- Consumables ---
    public static readonly Item HealingPotion =
        new("Healing Potion", ItemSlot.Consumable, Value: 40, Consumable: ConsumableEffect.Heal, Power: 14);
    public static readonly Item ManaDraught =
        new("Mana Draught", ItemSlot.Consumable, Value: 50, Consumable: ConsumableEffect.RestoreSpellPoints, Power: 10);
    public static readonly Item Antidote =
        new("Antidote", ItemSlot.Consumable, Value: 30, Consumable: ConsumableEffect.Cure, Power: 0);
    public static readonly Item ResurrectionDust =
        new("Resurrection Dust", ItemSlot.Consumable, Value: 200, Consumable: ConsumableEffect.Revive, Power: 1);

    /// <summary>Consumables stocked at Garth's Equipment Shoppe.</summary>
    public static readonly IReadOnlyList<Item> Potions =
        new[] { HealingPotion, ManaDraught, Antidote, ResurrectionDust };

    /// <summary>Produces an enchanted "+N" variant of a base weapon or piece of armour.</summary>
    public static Item Enchant(Item baseItem, int bonus)
    {
        var isWeapon = baseItem.Slot == ItemSlot.Weapon;
        var isArmor = baseItem.Slot is ItemSlot.Armor or ItemSlot.Shield;
        return baseItem with
        {
            Name = $"{baseItem.Name} +{bonus}",
            DamageBonus = baseItem.DamageBonus + (isWeapon ? bonus : 0),
            ArmorBonus = baseItem.ArmorBonus + (isArmor ? bonus : 0),
            Value = baseItem.Value * (bonus + 1) + bonus * 50,
            MagicBonus = bonus
        };
    }

    private static readonly Item[] EnchantableBases =
        { Dagger, ShortSword, LongSword, BattleAxe, LeatherArmor, ChainMail, PlateMail, SmallShield };

    /// <summary>Every enchanted "+1/+2/+3" variant that can drop as treasure.</summary>
    public static readonly IReadOnlyList<Item> MagicItems =
        (from b in EnchantableBases from n in new[] { 1, 2, 3 } select Enchant(b, n)).ToList();

    /// <summary>Mangar's signature staff — the legendary reward for slaying the Mad Wizard.</summary>
    public static readonly Item MangarsStaff =
        new("Mangar's Staff", ItemSlot.Weapon, DamageDice: 2, DamageSides: 8, DamageBonus: 5, Value: 5000, MagicBonus: 5);

    /// <summary>Every known item, keyed by name — used to resolve items when loading a save.</summary>
    public static readonly IReadOnlyDictionary<string, Item> ByName = new[]
    {
        Fists, Dagger, ShortSword, LongSword, BattleAxe, Staff,
        Robes, LeatherArmor, ChainMail, PlateMail, SmallShield,
        HealingPotion, ManaDraught, Antidote, ResurrectionDust, MangarsStaff
    }.Concat(MagicItems).ToDictionary(i => i.Name);

    public static Item? Find(string? name)
        => name is not null && ByName.TryGetValue(name, out var item) ? item : null;
}
