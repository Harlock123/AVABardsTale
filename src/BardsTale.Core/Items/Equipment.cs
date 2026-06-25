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
            case ItemSlot.Ring:
                // Prefer a free ring slot; if both are taken, displace the weaker ring
                // (lower value) so the stronger one stays on.
                if (c.Ring1 is null) { c.Ring1 = item; return null; }
                if (c.Ring2 is null) { c.Ring2 = item; return null; }
                if (c.Ring2.Value < c.Ring1.Value)
                {
                    var oldR2 = c.Ring2;
                    c.Ring2 = item;
                    return oldR2;
                }
                var oldR1 = c.Ring1;
                c.Ring1 = item;
                return oldR1;
            case ItemSlot.Amulet:
                var oldAm = c.Amulet;
                c.Amulet = item;
                return oldAm;
            default:
                return null;
        }
    }
}
