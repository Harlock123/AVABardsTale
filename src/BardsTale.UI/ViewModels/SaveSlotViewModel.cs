using CommunityToolkit.Mvvm.ComponentModel;

namespace BardsTale.UI.ViewModels;

/// <summary>One row in the save/load slot picker.</summary>
public sealed partial class SaveSlotViewModel : ViewModelBase
{
    public SaveSlotViewModel(string slot, string displayName, bool isAutosave)
    {
        Slot = slot;
        DisplayName = displayName;
        IsAutosave = isAutosave;
    }

    public string Slot { get; }
    public string DisplayName { get; }

    /// <summary>The autosave slot can be loaded but not written to by hand.</summary>
    public bool IsAutosave { get; }

    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private bool _isOccupied;
}
