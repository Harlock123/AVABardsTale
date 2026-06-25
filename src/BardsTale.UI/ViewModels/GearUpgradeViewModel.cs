using BardsTale.Core.Characters;
using BardsTale.Core.Items;

namespace BardsTale.UI.ViewModels;

/// <summary>One forgeable piece of gear at the Smithy — an equipped or stashed weapon/armour/shield.</summary>
public sealed class GearUpgradeViewModel : ViewModelBase
{
    public GearUpgradeViewModel(Character? owner, string slotLabel, Item item, Item? upgrade, int gold, int embers)
    {
        Owner = owner;
        SlotLabel = slotLabel;
        Item = item;
        Upgrade = upgrade;
        Gold = gold;
        Embers = embers;
    }

    /// <summary>The hero wearing this, or null if it's loose in the stash.</summary>
    public Character? Owner { get; }
    public string SlotLabel { get; }
    public Item Item { get; }

    /// <summary>The next "+N" version, or null if it can't be forged further.</summary>
    public Item? Upgrade { get; }
    public int Gold { get; }
    public int Embers { get; }

    public bool CanUpgrade => Upgrade is not null;
    public string Line => $"{SlotLabel}: {Item.Name}";
    public string UpgradeLabel => Upgrade is null ? "At its peak" : $"Forge → {Upgrade.Name}";
    public string CostText => Upgrade is null ? "" : $"{Gold} gold · {Embers} ember{(Embers == 1 ? "" : "s")}";
}
