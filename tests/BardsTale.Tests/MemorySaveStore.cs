using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BardsTale.Core.Game;
using BardsTale.UI.Services;

namespace BardsTale.Tests;

/// <summary>An in-memory <see cref="ISaveStore"/> for tests — only the text KV is implemented.</summary>
public sealed class MemorySaveStore : ISaveStore
{
    private readonly Dictionary<string, string> _kv = new();

    public IReadOnlyList<string> ManualSlots => Array.Empty<string>();
    public Task<bool> ExistsAsync(string slot) => Task.FromResult(_kv.ContainsKey(slot));
    public Task SaveAsync(GameSession session, string slot) => Task.CompletedTask;
    public Task<GameSession> LoadAsync(string slot) => throw new NotSupportedException();
    public Task<string> DescribeAsync(string slot) => Task.FromResult("");
    public Task<string?> LoadTextAsync(string key) => Task.FromResult(_kv.TryGetValue(key, out var v) ? v : null);
    public Task SaveTextAsync(string key, string value) { _kv[key] = value; return Task.CompletedTask; }
}
