namespace BardsTale.UI.ViewModels;

/// <summary>One selectable choice in a dungeon-event scene — its label and its index for resolution.</summary>
public sealed class DungeonEventOptionViewModel
{
    public DungeonEventOptionViewModel(int index, string label)
    {
        Index = index;
        Label = label;
    }

    public int Index { get; }
    public string Label { get; }
}
