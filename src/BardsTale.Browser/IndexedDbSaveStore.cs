using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using BardsTale.Core.Game;
using BardsTale.Core.Persistence;
using BardsTale.UI.Services;

namespace BardsTale.Browser;

/// <summary>
/// Browser <see cref="ISaveStore"/> that persists each save slot as a JSON string in
/// IndexedDB (via the saveStore.js module). Storage is durable across reloads but
/// scoped to this browser and origin — it does not roam between devices or browsers.
/// </summary>
public sealed class IndexedDbSaveStore : ISaveStore
{
    public IReadOnlyList<string> ManualSlots { get; } = SaveSlots.Manual;

    public async Task<bool> ExistsAsync(string slot) => await SaveStoreInterop.Get(slot) is not null;

    public Task SaveAsync(GameSession session, string slot) =>
        SaveStoreInterop.Set(slot, GameSerializer.ToJson(session));

    public async Task<GameSession> LoadAsync(string slot)
    {
        var json = await SaveStoreInterop.Get(slot);
        if (json is null)
            throw new System.InvalidOperationException($"Save slot '{slot}' is empty.");
        return GameSerializer.FromJson(json);
    }

    public async Task<string> DescribeAsync(string slot) => SaveSummary.Describe(await SaveStoreInterop.Get(slot));

    // Settings and other small values live under a "cfg:" prefix, separate from save slots.
    public Task<string?> LoadTextAsync(string key) => SaveStoreInterop.Get("cfg:" + key);
    public Task SaveTextAsync(string key, string value) => SaveStoreInterop.Set("cfg:" + key, value);

    /// <summary>Requests durable storage so saves resist browser eviction. Best-effort.</summary>
    public static Task<bool> RequestPersistentStorageAsync() => SaveStoreInterop.RequestPersist();
}

/// <summary>Bindings to wwwroot/saveStore.js, imported once at startup (see Program.cs).</summary>
internal static partial class SaveStoreInterop
{
    [JSImport("get", "saveStore")]
    public static partial Task<string?> Get(string key);

    [JSImport("set", "saveStore")]
    public static partial Task Set(string key, string value);

    [JSImport("remove", "saveStore")]
    public static partial Task Remove(string key);

    [JSImport("requestPersist", "saveStore")]
    public static partial Task<bool> RequestPersist();
}
