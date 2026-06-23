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
    public string CountText => Group.IsDefeated ? "defeated" : $"{Group.LivingCount} remaining";
    public bool IsDefeated => Group.IsDefeated;
    public string Label => $"{Index + 1}. {Name} ×{Group.LivingCount}";

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
    }
}
