using Avalonia.Media;
using BardsTale.Core.Combat;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BardsTale.UI.ViewModels;

/// <summary>Display wrapper for one enemy group on the combat screen.</summary>
public sealed partial class MonsterGroupViewModel : ViewModelBase
{
    public MonsterGroupViewModel(MonsterGroup group, int index)
    {
        Group = group;
        Index = index;
    }

    public MonsterGroup Group { get; }
    public int Index { get; }

    public string Name => Group.Name;
    public bool IsElite => Group.Template.IsElite;
    public MonsterAffix Affix => Group.Template.Affix;
    public string CountText => Group.IsDefeated ? "defeated" : $"{Group.LivingCount} remaining";
    public bool IsDefeated => Group.IsDefeated;

    public string Label
    {
        get
        {
            var affix = Affixes.Label(Affix);
            var prefix = affix.Length > 0 ? affix + " " : "";
            return $"{Index + 1}. {prefix}{Name} ×{Group.LivingCount}";
        }
    }

    /// <summary>Status glyphs for the group — 💤 if any are asleep, ☠ if any are poisoned.</summary>
    public string StatusGlyph
    {
        get
        {
            var live = Group.Monsters.Where(m => !m.IsDead).ToList();
            var glyph = "";
            if (live.Any(m => m.IsAsleep)) glyph += "💤";
            if (live.Any(m => m.IsPoisoned)) glyph += "☠";
            return glyph;
        }
    }

    public bool HasStatus => StatusGlyph.Length > 0;

    /// <summary>Elites read in gold, affixed foes in pale cyan — both stand out from the rabble.</summary>
    public IBrush LabelBrush => IsElite
        ? new SolidColorBrush(Color.Parse("#E8C56B"))
        : Affix != MonsterAffix.None
            ? new SolidColorBrush(Color.Parse("#9BD7E0"))
            : new SolidColorBrush(Color.Parse("#E8E9F0"));

    // --- Floating combat number (pops on the group when it's struck or mended) ---
    [ObservableProperty] private string _floatingText = "";
    [ObservableProperty] private IBrush _floatingBrush = Brushes.White;
    [ObservableProperty] private long _floatingPulse;

    /// <summary>Bumped when the group is struck, to flash the card (combat juice).</summary>
    [ObservableProperty] private long _hitFlashPulse;

    /// <summary>Shows a floating "-N" (damage, amber) or "+N" (heal, green) over this group.</summary>
    public void Pop(int delta)
    {
        if (delta == 0) return;
        FloatingText = delta < 0 ? delta.ToString() : "+" + delta;
        FloatingBrush = delta < 0 ? FloatHit : FloatHeal;
        FloatingPulse++;
        if (delta < 0) HitFlashPulse++; // a blow landed — flash the group
    }

    private static readonly IBrush FloatHit = new SolidColorBrush(Color.Parse("#E0A85A"));
    private static readonly IBrush FloatHeal = new SolidColorBrush(Color.Parse("#7FB069"));

    public string HealthText
    {
        get
        {
            var hp = Group.Monsters.Where(m => !m.IsDead).Sum(m => m.HitPoints);
            var max = Group.Monsters.Where(m => !m.IsDead).Sum(m => m.Template.MaxHitPoints);
            return Group.IsDefeated ? "—" : $"HP {hp}/{max}";
        }
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(IsDefeated));
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(HealthText));
        OnPropertyChanged(nameof(StatusGlyph));
        OnPropertyChanged(nameof(HasStatus));
    }
}
