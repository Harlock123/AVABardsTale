using BardsTale.Core.Characters;

namespace BardsTale.Core.Items;

/// <summary>Rules for whether a character may equip an item, and the act of equipping.</summary>
public static class Equipment
{
    public static bool CanEquip(Character c, Item item, out string reason)
    {
        reason = "";
        var heavyArmor = item.Slot is ItemSlot.Armor && item.ArmorBonus >= 4;
        if (heavyArmor && !c.Definition.CanWearHeavyArmor)
        {
            reason = $"A {c.Definition.Name} cannot wear {item.Name}.";
            return false;
        }

        // Spellcasters can only handle light weapons.
        if (item.Slot is ItemSlot.Weapon && c.IsSpellcaster && item.DamageSides > 6)
        {
            reason = $"A {c.Definition.Name} cannot wield {item.Name}.";
            return false;
        }

        return true;
    }

    /// <summary>Equips the item into its slot, returning the item that was displaced (if any).</summary>
    public static Item? Equip(Character c, Item item)
    {
        switch (item.Slot)
        {
            case ItemSlot.Weapon:
                var oldW = c.Weapon;
                c.Weapon = item;
                return oldW;
            case ItemSlot.Armor:
                var oldA = c.Armor;
                c.Armor = item;
                return oldA;
            case ItemSlot.Shield:
                var oldS = c.Shield;
                c.Shield = item;
                return oldS;
            default:
                return null;
        }
    }
}
