using System;
using System.Linq;
using System.Threading.Tasks;
using BardsTale.UI.Services;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

/// <summary>Covers the run-history dashboard: recording, ordering, the cap, round-trip, and totals.</summary>
public class HistoryDashboardTests
{
    private static RunRecord Run(string outcome, int depth, int score) =>
        new(outcome, depth, score, Ironman: false, Ascension: 0, Difficulty: 1, ChallengeSeed: -1,
            MonstersSlain: depth * 5, GoldEarned: depth * 100, Date: 638_000_000_000_000_000L);

    [Fact]
    public async Task Runs_are_stored_newest_first()
    {
        var store = new MemorySaveStore();
        await RunHistory.AppendAsync(store, Run("Victory", 20, 30000));
        await RunHistory.AppendAsync(store, Run("Defeat", 8, 4000));

        var list = await RunHistory.LoadAsync(store);

        Assert.Equal(2, list.Count);
        Assert.Equal("Defeat", list[0].Outcome); // most recent first
        Assert.Equal(8, list[0].Depth);
    }

    [Fact]
    public async Task History_is_capped_at_fifty_keeping_the_newest()
    {
        var store = new MemorySaveStore();
        for (var i = 0; i < 60; i++)
            await RunHistory.AppendAsync(store, Run("Defeat", i, i * 10));

        var list = await RunHistory.LoadAsync(store);

        Assert.Equal(50, list.Count);
        Assert.Equal(59, list[0].Depth); // the last appended is retained
    }

    [Fact]
    public async Task A_run_record_round_trips_through_the_store()
    {
        var store = new MemorySaveStore();
        var rec = new RunRecord("Victory", 20, 25400, Ironman: true, Ascension: 2, Difficulty: 2,
            ChallengeSeed: 20260627, MonstersSlain: 1240, GoldEarned: 99999, Date: 638_111_222_333_444_555L);

        await RunHistory.AppendAsync(store, rec);
        var loaded = (await RunHistory.LoadAsync(store)).Single();

        Assert.Equal(rec, loaded); // record value-equality across every field
    }

    [Fact]
    public void Totals_summarise_the_runs()
    {
        var vm = new HistoryViewModel(new[]
        {
            Run("Victory", 20, 30000), Run("Defeat", 8, 4000), Run("Victory", 15, 12000),
        });

        Assert.True(vm.HasRuns);
        Assert.Equal(3, vm.Runs.Count);
        Assert.Contains("3 runs", vm.Totals);
        Assert.Contains("2 victories", vm.Totals);
        Assert.Contains("floor 20", vm.Totals);   // deepest reached
    }

    [Fact]
    public void An_empty_history_reads_gracefully()
    {
        var vm = new HistoryViewModel(Array.Empty<RunRecord>());
        Assert.False(vm.HasRuns);
        Assert.Contains("No runs", vm.Totals);
    }

    [Fact]
    public void A_record_shows_its_outcome_and_tags()
    {
        var ironChallenge = new RunRecord("Defeat", 12, 13000, Ironman: true, Ascension: 1, Difficulty: 2,
            ChallengeSeed: 20260627, MonstersSlain: 60, GoldEarned: 4200, Date: 638_000_000_000_000_000L);
        var vm = new RunRecordViewModel(ironChallenge);

        Assert.False(vm.IsVictory);
        Assert.Contains("Defeat", vm.OutcomeText);
        Assert.Contains("Daily #20260627", vm.Tags);
        Assert.Contains("NG+1", vm.Tags);
        Assert.Contains("Ironman", vm.Tags);
        Assert.Contains("Hard", vm.Tags);
    }
}
