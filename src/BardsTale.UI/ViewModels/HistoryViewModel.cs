using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using BardsTale.Core.Combat;
using BardsTale.UI.Services;

namespace BardsTale.UI.ViewModels;

/// <summary>The run-history dashboard: past runs (newest first) and lifetime totals.</summary>
public sealed class HistoryViewModel : ViewModelBase
{
    public HistoryViewModel(IReadOnlyList<RunRecord> records)
    {
        Runs = new ObservableCollection<RunRecordViewModel>(records.Select(r => new RunRecordViewModel(r)));

        var count = records.Count;
        if (count == 0)
        {
            Totals = "No runs recorded yet — win the game or fall in an Ironman run to log one.";
            return;
        }

        var wins = records.Count(r => r.Outcome == "Victory");
        var deepest = records.Max(r => r.Depth);
        var best = records.Max(r => r.Score);
        var slain = records.Sum(r => (long)r.MonstersSlain);
        Totals = $"{count} run{(count == 1 ? "" : "s")}  ·  {wins} victor{(wins == 1 ? "y" : "ies")}  ·  "
                 + $"deepest floor {deepest}  ·  best score {best:N0}  ·  {slain:N0} monsters slain";
    }

    public ObservableCollection<RunRecordViewModel> Runs { get; }
    public string Totals { get; }
    public bool HasRuns => Runs.Count > 0;
}

/// <summary>One row in the history dashboard.</summary>
public sealed class RunRecordViewModel
{
    private readonly RunRecord _r;

    public RunRecordViewModel(RunRecord r) => _r = r;

    public bool IsVictory => _r.Outcome == "Victory";
    public string OutcomeText => IsVictory ? "🏆 Victory" : "☠ Defeat";
    public IBrush OutcomeBrush => IsVictory
        ? new SolidColorBrush(Color.Parse("#E8C56B"))
        : new SolidColorBrush(Color.Parse("#C0566B"));

    public string Summary => $"Floor {_r.Depth}  ·  Score {_r.Score:N0}";

    public string Tags
    {
        get
        {
            var tags = new List<string>();
            if (_r.ChallengeSeed >= 0) tags.Add($"🎯 Daily #{_r.ChallengeSeed}");
            if (_r.Ascension > 0) tags.Add($"NG+{_r.Ascension}");
            if (_r.Ironman) tags.Add("☠ Ironman");
            var diff = (Difficulty)_r.Difficulty;
            if (diff != Difficulty.Normal) tags.Add(diff.ToString());
            tags.Add($"{_r.MonstersSlain} slain · {_r.GoldEarned:N0} gold");
            return string.Join("   ·   ", tags);
        }
    }

    public string Date
    {
        get
        {
            try { return new DateTime(_r.Date).ToString("yyyy-MM-dd HH:mm"); }
            catch { return ""; }
        }
    }
}
