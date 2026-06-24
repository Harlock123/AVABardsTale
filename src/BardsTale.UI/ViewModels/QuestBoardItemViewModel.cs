using BardsTale.Core.Quests;

namespace BardsTale.UI.ViewModels;

/// <summary>One notice pinned to the town board, with an Accept button in the panel.</summary>
public sealed class QuestBoardItemViewModel : ViewModelBase
{
    public QuestBoardItemViewModel(Quest quest) => Model = quest;

    /// <summary>The underlying quest, accepted into the journal when the player takes it.</summary>
    public Quest Model { get; }

    public string Title => Model.Title;
    public string GiverLine => $"Posted by {Model.GiverName}";
    public string Pitch => Model.Pitch;
    public string Objective => Model.Objective;
    public string RewardLine => $"Reward: {Model.RewardLine}";
    public string TurnInHint => Model.TurnInHint;
}
