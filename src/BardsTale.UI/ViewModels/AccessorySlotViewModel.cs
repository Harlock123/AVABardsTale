using Avalonia.Media;
using BardsTale.Core.Items;

namespace BardsTale.UI.ViewModels;

/// <summary>
/// One of a hero's three accessory slots (Ring 1 / Ring 2 / Amulet) as shown in the
/// equip panel at Garth's: what's worn, whether the slot is empty, and whether the
/// currently-selected stash item could be equipped here.
/// </summary>
public sealed class AccessorySlotViewModel : ViewModelBase
{
    public AccessorySlotViewModel(string slotLabel, Item? item, bool canEquipSelected)
    {
        SlotLabel = slotLabel;
        Item = item;
        CanEquipSelected = canEquipSelected;
    }

    public string SlotLabel { get; }
    public Item? Item { get; }

    public bool HasItem => Item is not null;

    /// <summary>True when the stash item the player has selected fits this slot.</summary>
    public bool CanEquipSelected { get; }

    public string ItemText => Item is null ? "— empty —" : Item.DisplayName;
    public string DetailText => Item is null ? "" : Item.AccessoryText;

    public IBrush ItemBrush => Item is null
        ? new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80))
        : Item.IsMagic ? Brushes.Gold : new SolidColorBrush(Color.FromRgb(0xE8, 0xE9, 0xF0));
}
