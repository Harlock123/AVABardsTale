using BardsTale.Core.Characters;
using BardsTale.Core.Magic;

namespace BardsTale.Core.Items;

public enum ItemSlot
{
    None,
    Weapon,
    Armor,
    Shield,
    Consumable,
    /// <summary>A crafting material (e.g. forge embers) — carried, never equipped or quaffed.</summary>
    Material
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
    bool Identified = true,
    Spell? ItemPower = null)
{
    public bool IsWeapon => Slot == ItemSlot.Weapon;
    public bool IsConsumable => Slot == ItemSlot.Consumable;
    public bool IsMagic => MagicBonus > 0 || ItemPower is not null;

    /// <summary>A wielded item with a once-per-fight magical power (a wand, staff or rod).</summary>
    public bool HasPower => ItemPower is not null;

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

    /// <summary>The next "+N" version of an enchantable item, or null if it can't be upgraded (capped at +3).</summary>
    public static Item? UpgradeOf(Item item)
    {
        var baseName = item.Name;
        var plus = baseName.LastIndexOf(" +", System.StringComparison.Ordinal);
        if (plus >= 0) baseName = baseName[..plus];
        var baseItem = EnchantableBases.FirstOrDefault(b => b.Name == baseName);
        if (baseItem is null) return null;
        var next = item.MagicBonus + 1;
        return next <= 3 ? Enchant(baseItem, next) : null;
    }

    /// <summary>What the Smithy charges to forge the next enchantment onto an item: gold and forge embers.</summary>
    public static (int Gold, int Embers) UpgradeCost(Item item)
    {
        var target = item.MagicBonus + 1; // 1, 2 or 3
        return (Gold: 150 * target * target, Embers: target);
    }

    /// <summary>Mangar's signature staff — the legendary reward for slaying the Mad Wizard.</summary>
    public static readonly Item MangarsStaff =
        new("Mangar's Staff", ItemSlot.Weapon, DamageDice: 2, DamageSides: 8, DamageBonus: 5, Value: 5000, MagicBonus: 5);

    // --- Crafting material: drops in the deep, spent at the Smithy ---
    public static readonly Item ForgeEmber = new("Forge Ember", ItemSlot.Material, Value: 60);

    // --- Powered items: a wielded weapon (light enough for casters) with a once-per-fight power ---
    public static readonly Item WandOfFlames = new("Wand of Flames", ItemSlot.Weapon, 1, 4, 0, 0, 600, MagicBonus: 1,
        ItemPower: new Spell("PWR_FLAME", "FLAM", "Flame Burst", MagicSchool.Magician, 0, 0,
            SpellEffect.DamageEnemy, SpellTarget.SingleEnemy, 18, "A gout of fire erupts from the wand."));
    public static readonly Item WandOfFrost = new("Wand of Frost", ItemSlot.Weapon, 1, 4, 0, 0, 900, MagicBonus: 1,
        ItemPower: new Spell("PWR_FROST", "FRST", "Frost Lance", MagicSchool.Magician, 0, 0,
            SpellEffect.DamageEnemy, SpellTarget.SingleEnemy, 24, "A lance of ice skewers a foe."));
    public static readonly Item StaffOfStorms = new("Staff of Storms", ItemSlot.Weapon, 1, 6, 0, 0, 1400, MagicBonus: 1,
        ItemPower: new Spell("PWR_STORM", "STRM", "Thunderstrike", MagicSchool.Wizard, 0, 0,
            SpellEffect.DamageAllEnemies, SpellTarget.AllEnemies, 13, "Lightning forks across every foe."));
    public static readonly Item StaffOfRuin = new("Staff of Ruin", ItemSlot.Weapon, 1, 6, 0, 0, 2200, MagicBonus: 2,
        ItemPower: new Spell("PWR_RUIN", "RUIN", "Ruinous Bolt", MagicSchool.Wizard, 0, 0,
            SpellEffect.DamageEnemy, SpellTarget.SingleEnemy, 32, "A bolt of annihilating force."));
    public static readonly Item RodOfMending = new("Rod of Mending", ItemSlot.Weapon, 1, 4, 0, 0, 1100, MagicBonus: 1,
        ItemPower: new Spell("PWR_MEND", "MEND", "Renewal", MagicSchool.Conjurer, 0, 0,
            SpellEffect.HealParty, SpellTarget.Party, 16, "A wave of restoring light mends the party."));
    public static readonly Item ScepterOfGrace = new("Scepter of Grace", ItemSlot.Weapon, 1, 6, 0, 0, 900, MagicBonus: 1,
        ItemPower: new Spell("PWR_GRACE", "GRAC", "Mending Touch", MagicSchool.Conjurer, 0, 0,
            SpellEffect.HealAlly, SpellTarget.SingleAlly, 22, "Channels healing into one companion."));

    /// <summary>Powered items that can drop as treasure on the deeper floors.</summary>
    public static readonly IReadOnlyList<Item> PowerItems =
        new[] { WandOfFlames, WandOfFrost, StaffOfStorms, StaffOfRuin, RodOfMending, ScepterOfGrace };

    /// <summary>Every known item, keyed by name — used to resolve items when loading a save.</summary>
    public static readonly IReadOnlyDictionary<string, Item> ByName = new[]
    {
        Fists, Dagger, ShortSword, LongSword, BattleAxe, Staff,
        Robes, LeatherArmor, ChainMail, PlateMail, SmallShield,
        HealingPotion, ManaDraught, Antidote, ResurrectionDust, MangarsStaff, ForgeEmber
    }.Concat(MagicItems).Concat(PowerItems).ToDictionary(i => i.Name);

    public static Item? Find(string? name)
        => name is not null && ByName.TryGetValue(name, out var item) ? item : null;
}
