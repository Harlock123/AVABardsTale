using Avalonia.Media;
using BardsTale.Core.Items;

namespace BardsTale.UI.ViewModels;

/// <summary>Display wrapper for one item on sale at Garth's Equipment Shoppe.</summary>
public sealed class ShopItemViewModel : ViewModelBase
{
    public ShopItemViewModel(Item item) => Item = item;

    public Item Item { get; }

    public string Name => Item.DisplayName;
    public bool IsMagic => Item.IsMagic;
    public bool IsUnidentified => !Item.Identified;

    /// <summary>Unidentified items read mysterious-purple; identified enchanted items glow gold.</summary>
    public IBrush NameBrush => !Item.Identified
        ? Brushes.MediumPurple
        : Item.IsMagic ? Brushes.Gold : new SolidColorBrush(Color.FromRgb(0xE8, 0xE9, 0xF0));
    public int Price => Item.Value;
    public string PriceText => $"{Item.Value} gp";

    public string SlotText => Item.Slot.ToString();

    public string StatsText => !Item.Identified
        ? "unknown properties — appraise to reveal"
        : Item.Slot switch
        {
            ItemSlot.Weapon => $"dmg {Item.DamageText}",
            ItemSlot.Armor or ItemSlot.Shield => $"+{Item.ArmorBonus} armor",
            ItemSlot.Consumable => Item.EffectText,
            _ => ""
        };

    public string Label => $"{Name}  ({StatsText})";
}
