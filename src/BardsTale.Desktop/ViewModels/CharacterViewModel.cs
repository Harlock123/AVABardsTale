using Avalonia.Media;
using BardsTale.Core.Characters;
using BardsTale.Core.Game;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BardsTale.Desktop.ViewModels;

/// <summary>Presents a single <see cref="Character"/> for the roster panel.</summary>
public sealed partial class CharacterViewModel : ViewModelBase
{
    public CharacterViewModel(Character character)
    {
        Model = character;
    }

    public Character Model { get; }

    public string Name => Model.Name;
    public string ClassLine => $"{Races.Get(Model.Race).Name} {Model.Definition.Name}";
    public int Level => Model.Level;

    public string Health => $"{Model.HitPoints}/{Model.MaxHitPoints}";
    public string Spell => Model.IsSpellcaster ? $"{Model.SpellPoints}/{Model.MaxSpellPoints}" : "—";
    public bool IsSpellcaster => Model.IsSpellcaster;

    public double HealthFraction => Model.MaxHitPoints == 0 ? 0 : (double)Model.HitPoints / Model.MaxHitPoints;
    public bool IsDead => Model.IsDead;

    public string StatusText => Model.StatusTag;
    public bool HasAilment => Model.HasAilment;

    public IBrush StatusBrush => Model.StatusTag switch
    {
        "DEAD" => Brushes.Gray,
        "POISON" => Brushes.YellowGreen,
        "SLEEP" => Brushes.MediumPurple,
        "PARA" => Brushes.Orange,
        "HURT" => Brushes.IndianRed,
        _ => new SolidColorBrush(Color.FromRgb(0x8F, 0xB7, 0xFF))
    };

    public bool CanLevelUp => Progression.CanLevelUp(Model);
    public bool IsDrained => Model.IsDrained;
    public string DrainedText => Model.IsDrained ? $"drained −{Model.DrainedLevels} level(s)" : "";
    public string RestoreCostText => $"Restore ({200 * Model.DrainedLevels} gold)";

    public bool IsStatDrained => Model.HasDrainedStats;
    public string DrainedStatsText => string.Join(", ",
        System.Enum.GetValues<BardsTale.Core.Characters.Attribute>()
            .Where(a => Model.DrainedAttributes[a] > 0)
            .Select(a => $"−{Model.DrainedAttributes[a]} {AttributeSet.Abbreviation(a)}"));
    public string RestoreStatsCostText => $"Restore ({50 * Model.TotalDrainedStats} gold)";
    public string ExperienceText => $"XP {Model.Experience}/{Model.ExperienceForNextLevel}";
    public string GearText => $"{Model.Weapon?.Name ?? "—"} · {Model.Armor?.Name ?? "—"}"
        + (Model.Shield is null ? "" : $" · {Model.Shield.Name}");
    public string ArmorClassText => $"AC {Model.ArmorClass}";

    /// <summary>Re-reads all derived values after the underlying model changes.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(ClassLine));
        OnPropertyChanged(nameof(Level));
        OnPropertyChanged(nameof(Health));
        OnPropertyChanged(nameof(Spell));
        OnPropertyChanged(nameof(HealthFraction));
        OnPropertyChanged(nameof(IsDead));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HasAilment));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(CanLevelUp));
        OnPropertyChanged(nameof(IsDrained));
        OnPropertyChanged(nameof(DrainedText));
        OnPropertyChanged(nameof(RestoreCostText));
        OnPropertyChanged(nameof(IsStatDrained));
        OnPropertyChanged(nameof(DrainedStatsText));
        OnPropertyChanged(nameof(RestoreStatsCostText));
        OnPropertyChanged(nameof(ExperienceText));
        OnPropertyChanged(nameof(GearText));
        OnPropertyChanged(nameof(ArmorClassText));
    }
}
