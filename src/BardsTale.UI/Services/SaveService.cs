using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BardsTale.Core.Game;
using BardsTale.Core.Persistence;

namespace BardsTale.UI.Services;

/// <summary>
/// File-backed <see cref="ISaveStore"/> for the desktop heads: named save slots
/// (plus a dedicated autosave) written as JSON files under the user's app-data dir.
/// Its synchronous methods remain for direct/test use; the async <see cref="ISaveStore"/>
/// members wrap them and complete synchronously (no thread hops).
/// </summary>
public sealed class SaveService : ISaveStore
{
    public const string AutosaveSlot = SaveSlots.Autosave;

    private readonly string _dir;

    public SaveService(string? directory = null)
    {
        _dir = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BardsTale");
        Directory.CreateDirectory(_dir);
    }

    public IReadOnlyList<string> ManualSlots { get; } = SaveSlots.Manual;

    /// <summary>The folder these saves live in (for the "reveal save folder" action).</summary>
    public string? Location => _dir;

    private string PathFor(string slot) => Path.Combine(_dir, slot + ".json");

    public bool Exists(string slot) => File.Exists(PathFor(slot));

    public void Save(GameSession session, string slot) =>
        File.WriteAllText(PathFor(slot), GameSerializer.ToJson(session));

    public GameSession Load(string slot) => GameSerializer.FromJson(File.ReadAllText(PathFor(slot)));

    /// <summary>A short human-readable summary of a slot's contents for the slot picker.</summary>
    public string Describe(string slot) =>
        Exists(slot) ? SaveSummary.Describe(File.ReadAllText(PathFor(slot))) : "— empty —";

    // --- ISaveStore (async) — synchronous under the hood for the file backend ---

    public Task<bool> ExistsAsync(string slot) => Task.FromResult(Exists(slot));
    public Task SaveAsync(GameSession session, string slot) { Save(session, slot); return Task.CompletedTask; }
    public Task<GameSession> LoadAsync(string slot) => Task.FromResult(Load(slot));
    public Task<string> DescribeAsync(string slot) => Task.FromResult(Describe(slot));

    public Task DeleteAsync(string slot)
    {
        try { if (Exists(slot)) File.Delete(PathFor(slot)); } catch { /* best-effort */ }
        return Task.CompletedTask;
    }

    public Task<string?> LoadTextAsync(string key)
    {
        var path = Path.Combine(_dir, key + ".json");
        return Task.FromResult(File.Exists(path) ? File.ReadAllText(path) : null);
    }

    public Task SaveTextAsync(string key, string value)
    {
        File.WriteAllText(Path.Combine(_dir, key + ".json"), value);
        return Task.CompletedTask;
    }
}
