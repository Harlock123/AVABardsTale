using System.Linq;
using Avalonia.Media;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BardsTale.UI.ViewModels;

/// <summary>Presents a single <see cref="Character"/> for the roster panel.</summary>
public sealed partial class CharacterViewModel : ViewModelBase
{
    public CharacterViewModel(Character character)
    {
        Model = character;
    }

    public Character Model { get; }

    /// <summary>Marching-order rank ("Front"/"Back"), set by the roster when the list is built.</summary>
    public string RankText { get; set; } = "";

    /// <summary>True when this hero stands in the front rank — shown tinted in the roster.</summary>
    public bool IsFrontRank { get; set; }

    public IBrush RankBrush => IsFrontRank
        ? new SolidColorBrush(Color.FromRgb(0xE0, 0xA8, 0x5A))   // amber — in the thick of it
        : new SolidColorBrush(Color.FromRgb(0x7F, 0xB0, 0x69));  // green — shielded behind the line

    public string Name => Model.Name;
    public string ClassLine => $"{Races.Get(Model.Race).Name} {Model.Definition.Name}";
    public int Level => Model.Level;

    public string Health => $"{Model.HitPoints}/{Model.EffectiveMaxHitPoints}";
    public string Spell => Model.IsSpellcaster ? $"{Model.SpellPoints}/{Model.EffectiveMaxSpellPoints}" : "—";
    public bool IsSpellcaster => Model.IsSpellcaster;

    public double HealthFraction => Model.EffectiveMaxHitPoints == 0 ? 0 : (double)Model.HitPoints / Model.EffectiveMaxHitPoints;
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
    public string GearText
    {
        get
        {
            var parts = new System.Collections.Generic.List<string>
            {
                Model.Weapon?.Name ?? "—",
                Model.Armor?.Name ?? "—"
            };
            if (Model.Shield is not null) parts.Add(Model.Shield.Name);
            foreach (var accessory in Model.Accessories) parts.Add(accessory.Name);
            return string.Join(" · ", parts);
        }
    }
    public string ArmorClassText => $"AC {Model.ArmorClass}";

    // --- Floating combat number (pops on the roster when this hero is hit or healed) ---
    [ObservableProperty] private string _floatingText = "";
    [ObservableProperty] private IBrush _floatingBrush = Brushes.White;
    [ObservableProperty] private long _floatingPulse;

    /// <summary>Shows a floating "-N" (damage, red) or "+N" (heal, green) over this hero.</summary>
    public void Pop(int delta)
    {
        if (delta == 0) return;
        FloatingText = delta < 0 ? delta.ToString() : "+" + delta;
        FloatingBrush = delta < 0 ? FloatDamage : FloatHeal;
        FloatingPulse++;
    }

    private static readonly IBrush FloatDamage = new SolidColorBrush(Color.Parse("#E8746A"));
    private static readonly IBrush FloatHeal = new SolidColorBrush(Color.Parse("#7FB069"));

    /// <summary>The elements this hero's equipped gear wards against, e.g. "Wards: Fire, Cold".</summary>
    public string WardText
    {
        get
        {
            var wards = MonsterElements.DescribeGlyphs(Model.ResistedElements);
            return wards.Length > 0 ? $"Wards: {wards}" : "";
        }
    }

    public bool HasWards => Model.ResistedElements != Element.None;

    /// <summary>Active accessory set bonuses, e.g. "Set: Twin Bulwark".</summary>
    public string SetBonusText
    {
        get
        {
            var sets = Model.ActiveSets;
            return sets.Count > 0 ? "Set: " + string.Join(", ", sets.Select(s => s.Name)) : "";
        }
    }

    public bool HasSetBonus => Model.ActiveSets.Count > 0;

    /// <summary>A nudge toward completing a set the hero is one piece away from.</summary>
    public string SetHintText => Model.SetHints.Count > 0 ? "Almost: " + string.Join("  ·  ", Model.SetHints) : "";

    public bool HasSetHint => Model.SetHints.Count > 0;

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
        OnPropertyChanged(nameof(WardText));
        OnPropertyChanged(nameof(HasWards));
        OnPropertyChanged(nameof(SetBonusText));
        OnPropertyChanged(nameof(HasSetBonus));
        OnPropertyChanged(nameof(SetHintText));
        OnPropertyChanged(nameof(HasSetHint));
    }
}
