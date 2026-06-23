using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BardsTale.Core.Game;
using BardsTale.Core.Persistence;

namespace BardsTale.Desktop.Services;

/// <summary>Reads and writes named save slots (plus a dedicated autosave) as JSON files.</summary>
public sealed class SaveService
{
    public const string AutosaveSlot = "autosave";

    private readonly string _dir;

    public SaveService(string? directory = null)
    {
        _dir = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BardsTale");
        Directory.CreateDirectory(_dir);
    }

    /// <summary>The three player-managed save slots.</summary>
    public IReadOnlyList<string> ManualSlots { get; } = new[] { "slot1", "slot2", "slot3" };

    private string PathFor(string slot) => Path.Combine(_dir, slot + ".json");

    public bool Exists(string slot) => File.Exists(PathFor(slot));

    public void Save(GameSession session, string slot) =>
        File.WriteAllText(PathFor(slot), GameSerializer.ToJson(session));

    public GameSession Load(string slot) => GameSerializer.FromJson(File.ReadAllText(PathFor(slot)));

    /// <summary>A short human-readable summary of a slot's contents for the slot picker.</summary>
    public string Describe(string slot)
    {
        if (!Exists(slot)) return "— empty —";
        try
        {
            var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(PathFor(slot)));
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
