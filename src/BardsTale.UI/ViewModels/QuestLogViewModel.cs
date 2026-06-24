using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using BardsTale.Core.Quests;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BardsTale.UI.ViewModels;

/// <summary>
/// The quest-journal overlay: a snapshot of the party's active side quests with
/// their progress, where to turn each one in, and its reward. Rebuilt each time the
/// log is opened so it always reflects current progress.
/// </summary>
public sealed class QuestLogViewModel : ViewModelBase
{
    public QuestLogViewModel(QuestLog log)
    {
        Quests = new ObservableCollection<QuestEntryViewModel>(
            log.Active
                .OrderByDescending(q => q.Status == QuestStatus.ReadyToTurnIn)
                .Select(q => new QuestEntryViewModel(q)));
    }

    public ObservableCollection<QuestEntryViewModel> Quests { get; }

    public bool HasQuests => Quests.Count > 0;

    /// <summary>Drops a card from the journal view and refreshes the empty-state message.</summary>
    public void Remove(QuestEntryViewModel entry)
    {
        Quests.Remove(entry);
        OnPropertyChanged(nameof(HasQuests));
    }

    public string EmptyMessage =>
        "Your journal is empty. Ask around — Garth at the Equipment Shoppe, the regulars "
        + "in the taverns, and folk you meet on the streets may have work for you.";
}

/// <summary>A single quest line in the journal.</summary>
public sealed partial class QuestEntryViewModel : ViewModelBase
{
    private readonly Quest _quest;

    public QuestEntryViewModel(Quest quest) => _quest = quest;

    /// <summary>The underlying quest, so the journal can abandon it.</summary>
    public Quest Model => _quest;

    /// <summary>True while this card is showing its "really abandon?" confirmation row.</summary>
    [ObservableProperty] private bool _confirmingAbandon;

    [RelayCommand]
    private void RequestAbandon() => ConfirmingAbandon = true;

    [RelayCommand]
    private void CancelAbandon() => ConfirmingAbandon = false;

    public string Title => _quest.Title;
    public string Objective => _quest.Objective;
    public string Pitch => _quest.Pitch;
    public string GiverLine => $"From {_quest.GiverName}";
    public string RewardLine => $"Reward: {_quest.RewardLine}";
    public string TurnInHint => _quest.TurnInHint;

    public bool IsReady => _quest.Status == QuestStatus.ReadyToTurnIn;
    public string StatusText => IsReady ? "READY TO TURN IN" : "In progress";

    /// <summary>Gold border for "ready" quests, muted grey otherwise — tints the card.</summary>
    public IBrush AccentBrush => IsReady ? Gold : Slate;
    public IBrush StatusBrush => IsReady ? Green : Slate;

    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#E8C56B"));
    private static readonly IBrush Green = new SolidColorBrush(Color.Parse("#9BE89B"));
    private static readonly IBrush Slate = new SolidColorBrush(Color.Parse("#7E879B"));
}
