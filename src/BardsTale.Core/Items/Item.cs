using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Magic;

namespace BardsTale.Core.Items;

public enum ItemSlot
{
    None,
    Weapon,
    Armor,
    Shield,
    /// <summary>A ring — worn for protection and elemental wards. A hero wears up to two.</summary>
    Ring,
    /// <summary>An amulet or talisman — worn for protection and elemental wards. One per hero.</summary>
    Amulet,
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
    Spell? ItemPower = null,
    Element ResistsElement = Element.None,
    // --- Accessory effects (rings & amulets) ---
    int HitBonus = 0,
    int RegenPerRound = 0,
    int LuckBonus = 0,
    StatusEffect ImmuneStatus = StatusEffect.None,
    int MaxHitPointBonus = 0,
    int MaxSpellPointBonus = 0)
{
    public bool IsWeapon => Slot == ItemSlot.Weapon;
    public bool IsAccessory => Slot is ItemSlot.Ring or ItemSlot.Amulet;
    public bool IsConsumable => Slot == ItemSlot.Consumable;

    /// <summary>True when an accessory grants any benefit (armour, ward, or a combat effect).</summary>
    public bool HasAccessoryEffect => IsAccessory && (ArmorBonus > 0 || ResistsElement != Element.None
        || HitBonus > 0 || DamageBonus > 0 || RegenPerRound > 0 || LuckBonus > 0 || ImmuneStatus != StatusEffect.None
        || MaxHitPointBonus > 0 || MaxSpellPointBonus > 0);

    public bool IsMagic => MagicBonus > 0 || ItemPower is not null || ResistsElement != Element.None
        || HasAccessoryEffect;

    /// <summary>A wielded item with a once-per-fight magical power (a wand, staff or rod).</summary>
    public bool HasPower => ItemPower is not null;

    /// <summary>What the party sees: the true name once identified, a vague label until then.</summary>
    public string DisplayName => Identified ? Name : $"Unidentified {Category}";

    private string Category => Slot switch
    {
        ItemSlot.Weapon => "Weapon",
        ItemSlot.Armor => "Armor",
        ItemSlot.Shield => "Shield",
        ItemSlot.Ring => "Ring",
        ItemSlot.Amulet => "Amulet",
        _ => "Item"
    };

    /// <summary>What this item wards against, e.g. "wards Fire, Cold" — empty if it grants no resistance.</summary>
    public string ResistText
    {
        get
        {
            var named = MonsterElements.Describe(ResistsElement);
            return named.Length > 0 ? $"wards {named}" : "";
        }
    }

    /// <summary>A readable summary of an accessory's every benefit, e.g. "+1 armor · wards Fire · +2 regen".</summary>
    public string AccessoryText
    {
        get
        {
            var parts = new List<string>();
            if (ArmorBonus > 0) parts.Add($"+{ArmorBonus} armor");
            if (ResistsElement != Element.None) parts.Add(ResistText);
            if (MaxHitPointBonus > 0) parts.Add($"+{MaxHitPointBonus} max HP");
            if (MaxSpellPointBonus > 0) parts.Add($"+{MaxSpellPointBonus} max SP");
            if (DamageBonus > 0) parts.Add($"+{DamageBonus} dmg");
            if (HitBonus > 0) parts.Add($"+{HitBonus} to-hit");
            if (RegenPerRound > 0) parts.Add($"+{RegenPerRound} regen/round");
            if (LuckBonus > 0) parts.Add($"+{LuckBonus} luck");
            if (ImmuneStatus != StatusEffect.None) parts.Add($"immune to {DescribeStatuses(ImmuneStatus)}");
            return parts.Count > 0 ? string.Join(" · ", parts) : "trinket";
        }
    }

    /// <summary>Renders a status-effect flag set as a readable list ("paralysis, sleep").</summary>
    public static string DescribeStatuses(StatusEffect statuses)
    {
        var parts = new List<string>();
        if (statuses.HasFlag(StatusEffect.Paralyzed)) parts.Add("paralysis");
        if (statuses.HasFlag(StatusEffect.Asleep)) parts.Add("sleep");
        if (statuses.HasFlag(StatusEffect.Poisoned)) parts.Add("poison");
        return string.Join(", ", parts);
    }

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
        // Armour, shields, rings and amulets all soak the bonus into their armour value.
        var isArmor = baseItem.Slot is ItemSlot.Armor or ItemSlot.Shield or ItemSlot.Ring or ItemSlot.Amulet;
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

    /// <summary>An item's base name with any "+N" enchant suffix stripped ("Ring of Fire Ward +2" → "Ring of Fire Ward").</summary>
    public static string BaseName(string name)
    {
        var plus = name.LastIndexOf(" +", System.StringComparison.Ordinal);
        return plus >= 0 ? name[..plus] : name;
    }

    /// <summary>The next "+N" version of an enchantable item, or null if it can't be upgraded (capped at +3).</summary>
    public static Item? UpgradeOf(Item item)
    {
        var baseName = BaseName(item.Name);
        // Weapons/armour and warding accessories share the same +1/+2/+3 forge chain.
        var baseItem = EnchantableBases.FirstOrDefault(b => b.Name == baseName)
                       ?? Accessories.FirstOrDefault(b => b.Name == baseName);
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

    // --- Accessories: rings, amulets and talismans worn for protection and elemental wards.
    //     Base accessories carry no enchant; the Smithy can forge +1/+2/+3 onto them, each
    //     tier adding a point of armour while preserving the elemental ward. ---
    public static readonly Item RingOfProtection = new("Ring of Protection", ItemSlot.Ring,
        ArmorBonus: 1, Value: 500);
    public static readonly Item RingOfFireWard = new("Ring of Fire Ward", ItemSlot.Ring,
        Value: 700, ResistsElement: Element.Fire);
    public static readonly Item RingOfFrostWard = new("Ring of Frost Ward", ItemSlot.Ring,
        Value: 700, ResistsElement: Element.Cold);
    public static readonly Item RingOfStormWard = new("Ring of Storm Ward", ItemSlot.Ring,
        Value: 700, ResistsElement: Element.Lightning);
    public static readonly Item AmuletOfTheViper = new("Amulet of the Viper", ItemSlot.Amulet,
        Value: 700, ResistsElement: Element.Poison);
    public static readonly Item AmuletOfWarding = new("Amulet of Warding", ItemSlot.Amulet,
        ArmorBonus: 1, Value: 1800, ResistsElement: Element.Fire | Element.Cold | Element.Lightning);
    public static readonly Item TalismanOfTheAges = new("Talisman of the Ages", ItemSlot.Amulet,
        ArmorBonus: 1, Value: 3200,
        ResistsElement: Element.Fire | Element.Cold | Element.Lightning | Element.Poison | Element.Arcane);

    // --- Effect accessories: rings & amulets that boost combat rather than ward elements ---
    public static readonly Item RingOfRegeneration = new("Ring of Regeneration", ItemSlot.Ring,
        Value: 900, RegenPerRound: 2);
    public static readonly Item RingOfStriking = new("Ring of Striking", ItemSlot.Ring,
        Value: 800, DamageBonus: 2);
    public static readonly Item RingOfAccuracy = new("Ring of Accuracy", ItemSlot.Ring,
        Value: 800, HitBonus: 2);
    public static readonly Item RingOfFreeAction = new("Ring of Free Action", ItemSlot.Ring,
        Value: 1200, ImmuneStatus: StatusEffect.Paralyzed | StatusEffect.Asleep);
    public static readonly Item AmuletOfFortune = new("Amulet of Fortune", ItemSlot.Amulet,
        Value: 1000, LuckBonus: 4);
    public static readonly Item AmuletOfValor = new("Amulet of Valor", ItemSlot.Amulet,
        Value: 1300, HitBonus: 2, DamageBonus: 1);

    // --- Vitality accessories: bolster a hero's maximum hit points and spell points ---
    public static readonly Item RingOfVigor = new("Ring of Vigor", ItemSlot.Ring,
        Value: 1000, MaxHitPointBonus: 12);
    public static readonly Item AmuletOfVitality = new("Amulet of Vitality", ItemSlot.Amulet,
        Value: 1500, MaxHitPointBonus: 25);
    public static readonly Item AmuletOfTheMagi = new("Amulet of the Magi", ItemSlot.Amulet,
        Value: 1400, MaxSpellPointBonus: 14);

    /// <summary>Worn accessories that can be bought, sold, or turn up as treasure.</summary>
    public static readonly IReadOnlyList<Item> Accessories =
        new[]
        {
            RingOfProtection, RingOfFireWard, RingOfFrostWard, RingOfStormWard,
            AmuletOfTheViper, AmuletOfWarding, TalismanOfTheAges,
            RingOfRegeneration, RingOfStriking, RingOfAccuracy, RingOfFreeAction,
            AmuletOfFortune, AmuletOfValor,
            RingOfVigor, AmuletOfVitality, AmuletOfTheMagi
        };

    /// <summary>Every Smithy-forged "+N" accessory — registered so saved enchanted gear resolves on load.</summary>
    public static readonly IReadOnlyList<Item> EnchantedAccessories =
        (from a in Accessories from n in new[] { 1, 2, 3 } select Enchant(a, n)).ToList();

    // --- Crafting material: drops in the deep, spent at the Smithy ---
    public static readonly Item ForgeEmber = new("Forge Ember", ItemSlot.Material, Value: 60);

    // --- Powered items: a wielded weapon (light enough for casters) with a once-per-fight power ---
    public static readonly Item WandOfFlames = new("Wand of Flames", ItemSlot.Weapon, 1, 4, 0, 0, 600, MagicBonus: 1,
        ItemPower: new Spell("PWR_FLAME", "FLAM", "Flame Burst", MagicSchool.Magician, 0, 0,
            SpellEffect.DamageEnemy, SpellTarget.SingleEnemy, 18, "A gout of fire erupts from the wand.", Element.Fire));
    public static readonly Item WandOfFrost = new("Wand of Frost", ItemSlot.Weapon, 1, 4, 0, 0, 900, MagicBonus: 1,
        ItemPower: new Spell("PWR_FROST", "FRST", "Frost Lance", MagicSchool.Magician, 0, 0,
            SpellEffect.DamageEnemy, SpellTarget.SingleEnemy, 24, "A lance of ice skewers a foe.", Element.Cold));
    public static readonly Item StaffOfStorms = new("Staff of Storms", ItemSlot.Weapon, 1, 6, 0, 0, 1400, MagicBonus: 1,
        ItemPower: new Spell("PWR_STORM", "STRM", "Thunderstrike", MagicSchool.Wizard, 0, 0,
            SpellEffect.DamageAllEnemies, SpellTarget.AllEnemies, 13, "Lightning forks across every foe.", Element.Lightning));
    public static readonly Item StaffOfRuin = new("Staff of Ruin", ItemSlot.Weapon, 1, 6, 0, 0, 2200, MagicBonus: 2,
        ItemPower: new Spell("PWR_RUIN", "RUIN", "Ruinous Bolt", MagicSchool.Wizard, 0, 0,
            SpellEffect.DamageEnemy, SpellTarget.SingleEnemy, 32, "A bolt of annihilating force."));
    public static readonly Item RodOfMending = new("Rod of Mending", ItemSlot.Weapon, 1, 4, 0, 0, 1100, MagicBonus: 1,
        ItemPower: new Spell("PWR_MEND", "MEND", "Renewal", MagicSchool.Conjurer, 0, 0,
            SpellEffect.HealParty, SpellTarget.Party, 16, "A wave of restoring light mends the party."));
    public static readonly Item ScepterOfGrace = new("Scepter of Grace", ItemSlot.Weapon, 1, 6, 0, 0, 900, MagicBonus: 1,
        ItemPower: new Spell("PWR_GRACE", "GRAC", "Mending Touch", MagicSchool.Conjurer, 0, 0,
            SpellEffect.HealAlly, SpellTarget.SingleAlly, 22, "Channels healing into one companion."));

    // Encounter-long party buffs — invoked once, they shape the whole fight.
    public static readonly Item BannerOfHaste = new("Banner of Haste", ItemSlot.Weapon, 1, 6, 0, 0, 2600, MagicBonus: 1,
        ItemPower: new Spell("PWR_HASTE", "HAST", "Haste", MagicSchool.Sorcerer, 0, 0,
            SpellEffect.HasteParty, SpellTarget.Party, 1, "The whole party gains an extra attack each round."));
    public static readonly Item StandardOfRenewal = new("Standard of Renewal", ItemSlot.Weapon, 1, 4, 0, 0, 2200, MagicBonus: 1,
        ItemPower: new Spell("PWR_RENEW", "RENW", "Aura of Renewal", MagicSchool.Conjurer, 0, 0,
            SpellEffect.RegenParty, SpellTarget.Party, 8, "A healing aura mends the party each round."));
    public static readonly Item AegisBanner = new("Aegis Banner", ItemSlot.Weapon, 1, 6, 0, 0, 1600, MagicBonus: 1,
        ItemPower: new Spell("PWR_AEGIS", "AEGS", "Aegis", MagicSchool.Conjurer, 0, 0,
            SpellEffect.BuffPartyArmor, SpellTarget.Party, 3, "Wards the party, turning aside blows all fight."));
    public static readonly Item HornOfValor = new("Horn of Valor", ItemSlot.Weapon, 1, 6, 0, 0, 1600, MagicBonus: 1,
        ItemPower: new Spell("PWR_VALOR", "VALR", "Warcry", MagicSchool.Sorcerer, 0, 0,
            SpellEffect.BuffPartyAttack, SpellTarget.Party, 3, "A rousing blast sharpens the party's blows all fight."));
    public static readonly Item ChimeOfCleansing = new("Chime of Cleansing", ItemSlot.Weapon, 1, 4, 0, 0, 1200, MagicBonus: 1,
        ItemPower: new Spell("PWR_CLEAN", "CLNS", "Cleansing Peal", MagicSchool.Conjurer, 0, 0,
            SpellEffect.CleanseParty, SpellTarget.Party, 0, "A clear note cures the whole party of ailments."));
    public static readonly Item OrbOfMana = new("Orb of Mana", ItemSlot.Weapon, 1, 4, 0, 0, 1400, MagicBonus: 1,
        ItemPower: new Spell("PWR_MANA", "MANA", "Mana Font", MagicSchool.Wizard, 0, 0,
            SpellEffect.RestorePartySpellPoints, SpellTarget.Party, 14, "Restores spell points to the whole party."));
    public static readonly Item WandOfLeeching = new("Wand of Leeching", ItemSlot.Weapon, 1, 4, 0, 0, 1800, MagicBonus: 2,
        ItemPower: new Spell("PWR_LEECH", "LECH", "Soul Drain", MagicSchool.Sorcerer, 0, 0,
            SpellEffect.DrainEnemy, SpellTarget.SingleEnemy, 22, "Drains a foe's life into the wielder."));
    public static readonly Item RodOfResurrection = new("Rod of Resurrection", ItemSlot.Weapon, 1, 4, 0, 0, 2400, MagicBonus: 1,
        ItemPower: new Spell("PWR_RAISE", "RAIS", "Raise Ally", MagicSchool.Conjurer, 0, 0,
            SpellEffect.Revive, SpellTarget.SingleAlly, 12, "Calls a fallen companion back to life."));

    /// <summary>Powered items that can drop as treasure on the deeper floors.</summary>
    public static readonly IReadOnlyList<Item> PowerItems =
        new[]
        {
            WandOfFlames, WandOfFrost, StaffOfStorms, StaffOfRuin, RodOfMending, ScepterOfGrace,
            BannerOfHaste, StandardOfRenewal, AegisBanner, HornOfValor, ChimeOfCleansing, OrbOfMana,
            WandOfLeeching, RodOfResurrection
        };

    /// <summary>Every known item, keyed by name — used to resolve items when loading a save.</summary>
    public static readonly IReadOnlyDictionary<string, Item> ByName = new[]
    {
        Fists, Dagger, ShortSword, LongSword, BattleAxe, Staff,
        Robes, LeatherArmor, ChainMail, PlateMail, SmallShield,
        HealingPotion, ManaDraught, Antidote, ResurrectionDust, MangarsStaff, ForgeEmber
    }.Concat(MagicItems).Concat(PowerItems).Concat(Accessories).Concat(EnchantedAccessories)
        .ToDictionary(i => i.Name);

    public static Item? Find(string? name)
        => name is not null && ByName.TryGetValue(name, out var item) ? item : null;
}
