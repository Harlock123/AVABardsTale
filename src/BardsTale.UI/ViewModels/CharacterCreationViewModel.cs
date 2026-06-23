using System.Collections.Generic;
using System.Linq;
using BardsTale.Core.Characters;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BardsTale.UI.ViewModels;

/// <summary>
/// Drives the Adventurers Guild creation flow: pick name, race and class, roll
/// attributes (re-rollable), then hand the finished hero to the guild to recruit.
/// </summary>
public sealed partial class CharacterCreationViewModel : ViewModelBase
{
    private readonly CharacterFactory _factory;

    public CharacterCreationViewModel(CharacterFactory factory)
    {
        _factory = factory;
        Races = BardsTale.Core.Characters.Races.All.Values.ToList();
        Classes = BardsTale.Core.Characters.Classes.All.Values.ToList();
        SelectedRace = Races.First();
        SelectedClass = Classes.First();
    }

    public IReadOnlyList<RaceDefinition> Races { get; }
    public IReadOnlyList<ClassDefinition> Classes { get; }

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private RaceDefinition _selectedRace;
    [ObservableProperty] private ClassDefinition _selectedClass;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCandidate))]
    [NotifyPropertyChangedFor(nameof(PreviewName), nameof(PreviewClassLine), nameof(PreviewStrength),
        nameof(PreviewIntelligence), nameof(PreviewDexterity), nameof(PreviewConstitution),
        nameof(PreviewLuck), nameof(PreviewHealth), nameof(PreviewSpell), nameof(PreviewGear))]
    private Character? _candidate;

    public bool HasCandidate => Candidate is not null;
    private bool CanRoll() => !string.IsNullOrWhiteSpace(Name);

    partial void OnNameChanged(string value) => RollCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanRoll))]
    private void Roll()
    {
        Candidate = _factory.Create(Name.Trim(), SelectedRace.Race, SelectedClass.Class);
    }

    /// <summary>Clears the current draft after a hero has been recruited.</summary>
    public void Reset()
    {
        Candidate = null;
        Name = "";
    }

    public string PreviewName => Candidate?.Name ?? "—";
    public string PreviewClassLine => Candidate is null ? "" :
        $"{BardsTale.Core.Characters.Races.Get(Candidate.Race).Name} {Candidate.Definition.Name}";
    public string PreviewStrength => Stat(c => c.Attributes.Strength);
    public string PreviewIntelligence => Stat(c => c.Attributes.Intelligence);
    public string PreviewDexterity => Stat(c => c.Attributes.Dexterity);
    public string PreviewConstitution => Stat(c => c.Attributes.Constitution);
    public string PreviewLuck => Stat(c => c.Attributes.Luck);
    public string PreviewHealth => Candidate is null ? "—" : $"{Candidate.MaxHitPoints}";
    public string PreviewSpell => Candidate is null ? "—" : Candidate.IsSpellcaster ? $"{Candidate.MaxSpellPoints}" : "—";
    public string PreviewGear => Candidate is null ? "" :
        $"{Candidate.Weapon?.Name ?? "—"} · {Candidate.Armor?.Name ?? "—"}";

    private string Stat(System.Func<Character, int> sel) => Candidate is null ? "—" : sel(Candidate).ToString();
}
