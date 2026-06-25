using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using BardsTale.Core.Game;

namespace BardsTale.UI.ViewModels;

/// <summary>The achievements / renown overlay — a snapshot of standing and the full list.</summary>
public sealed class AchievementsViewModel : ViewModelBase
{
    public AchievementsViewModel(RenownLog renown)
    {
        Renown = renown.Renown;
        UnlockedCount = renown.UnlockedCount;
        Total = Achievements.All.Count;
        DiscountPercent = (int)Math.Round(renown.Discount * 100);
        Entries = new ObservableCollection<AchievementEntryViewModel>(
            Achievements.All
                .OrderByDescending(a => renown.IsUnlocked(a.Id))
                .Select(a => new AchievementEntryViewModel(a, renown.IsUnlocked(a.Id))));
    }

    public ObservableCollection<AchievementEntryViewModel> Entries { get; }

    public int Renown { get; }
    public int UnlockedCount { get; }
    public int Total { get; }
    public int DiscountPercent { get; }

    public string Heading => $"Renown {Renown}  ·  {UnlockedCount} / {Total} earned  ·  −{DiscountPercent}% on town services";
}

/// <summary>One achievement row.</summary>
public sealed class AchievementEntryViewModel : ViewModelBase
{
    private readonly Achievement _a;

    public AchievementEntryViewModel(Achievement a, bool unlocked)
    {
        _a = a;
        Unlocked = unlocked;
    }

    public bool Unlocked { get; }
    public string Name => Unlocked ? $"🏆 {_a.Name}" : $"🔒 {_a.Name}";
    public string Description => _a.Description;
    public string RenownText => $"+{_a.Renown}";
    public double Opacity => Unlocked ? 1.0 : 0.5;

    public IBrush NameBrush => Unlocked ? Gold : Slate;
    public IBrush RenownBrush => Unlocked ? Green : Slate;

    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#E8C56B"));
    private static readonly IBrush Green = new SolidColorBrush(Color.Parse("#9BE89B"));
    private static readonly IBrush Slate = new SolidColorBrush(Color.Parse("#7E879B"));
}
