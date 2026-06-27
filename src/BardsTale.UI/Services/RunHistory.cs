using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BardsTale.UI.Services;

/// <summary>One completed run, recorded for the history dashboard.</summary>
public sealed record RunRecord(
    string Outcome,     // "Victory" or "Defeat"
    int Depth,
    int Score,
    bool Ironman,
    int Ascension,
    int Difficulty,     // BardsTale.Core.Combat.Difficulty as an int
    int ChallengeSeed,  // -1 if not a daily challenge
    int MonstersSlain,
    int GoldEarned,
    long Date           // DateTime.Ticks
);

/// <summary>
/// A rolling log of completed runs (wins and Ironman deaths), stored in the platform save store's
/// text KV — independent of save slots, so it survives the Ironman save wipe. Newest first, capped.
/// </summary>
public static class RunHistory
{
    private const string Key = "history";
    private const int MaxRuns = 50;

    public static async Task<IReadOnlyList<RunRecord>> LoadAsync(ISaveStore store)
    {
        try
        {
            var json = await store.LoadTextAsync(Key);
            if (string.IsNullOrEmpty(json)) return Array.Empty<RunRecord>();
            return JsonSerializer.Deserialize<List<RunRecord>>(json) ?? new List<RunRecord>();
        }
        catch { return Array.Empty<RunRecord>(); }
    }

    public static async Task AppendAsync(ISaveStore store, RunRecord record)
    {
        var list = (await LoadAsync(store)).ToList();
        list.Insert(0, record); // newest first
        if (list.Count > MaxRuns) list = list.Take(MaxRuns).ToList();
        try { await store.SaveTextAsync(Key, JsonSerializer.Serialize(list)); }
        catch { /* best-effort */ }
    }
}
