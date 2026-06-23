using BardsTale.Core.Characters;

namespace BardsTale.Desktop.ViewModels;

/// <summary>A choosable party member when a spell needs a single-ally target.</summary>
public sealed class AllyTargetViewModel : ViewModelBase
{
    private readonly Character _character;

    public AllyTargetViewModel(Character character, int index, bool isValid)
    {
        _character = character;
        Index = index;
        IsValid = isValid;
    }

    public int Index { get; }
    public bool IsValid { get; }

    public string Name => _character.Name;
    public string Health => $"{_character.HitPoints}/{_character.MaxHitPoints}";
    public string StatusTag => _character.StatusTag;
}
