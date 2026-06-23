using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BardsTale.Core.Game;
using BardsTale.Core.Persistence;

namespace BardsTale.UI.Services;

/// <summary>
/// Platform-agnostic persistence for named save slots. The desktop heads back this
/// with files (<see cref="SaveService"/>); the browser head backs it with IndexedDB.
/// The API is async because browser storage is async — desktop implementations
/// complete synchronously (no thread hops), so callers stay responsive everywhere.
/// </summary>
public interface ISaveStore
{
    /// <summary>The player-managed save slots (the autosave slot is separate).</summary>
    IReadOnlyList<string> ManualSlots { get; }

    Task<bool> ExistsAsync(string slot);
    Task SaveAsync(GameSession session, string slot);
    Task<GameSession> LoadAsync(string slot);

    /// <summary>A short human-readable summary of a slot for the slot picker.</summary>
    Task<string> DescribeAsync(string slot);
}

/// <summary>Slot identifiers shared by every <see cref="ISaveStore"/> implementation.</summary>
public static class SaveSlots
{
    public const string Autosave = "autosave";
    public static readonly IReadOnlyList<string> Manual = new[] { "slot1", "slot2", "slot3" };
}

/// <summary>Builds the slot-picker summary line from raw save JSON, shared by all stores.</summary>
public static class SaveSummary
{
    public static string Describe(string? json)
    {
        if (string.IsNullOrEmpty(json)) return "— empty —";
        try
        {
            var data = JsonSerializer.Deserialize<SaveData>(json);
            if (data is null) return "— corrupt —";
            var where = data.Dungeon is { } d
                ? $"{d.Levels.FirstOrDefault(l => l.Depth == d.Depth)?.Name ?? "Catacombs"} (depth {d.Depth})"
                : "Skara Brae";
            return $"{data.Party.Members.Count} heroes · {data.Party.Gold} gold · {where}";
        }
        catch
        {
            return "— corrupt —";
        }
    }
}
