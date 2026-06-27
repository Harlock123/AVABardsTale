using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BardsTale.Core.Game;
using BardsTale.UI.Services;
using BardsTale.UI.Settings;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Covers the onboarding polish: the help guide shows on a first-ever launch (default on), and the
/// "show on startup" choice persists so unchecking it really is "don't show again".
/// </summary>
public class OnboardingTests
{
    [Fact]
    public void Show_help_on_startup_defaults_on_for_new_players()
        => Assert.True(new AppSettings().ShowHelpOnStartup);

    [Fact]
    public async Task A_fresh_install_with_no_settings_file_keeps_help_on()
    {
        var settings = new AppSettings();                 // default: show help on startup
        await SettingsService.LoadAsync(new MemoryStore(), settings); // empty store → nothing to load
        Assert.True(settings.ShowHelpOnStartup);          // so a first launch still shows the guide
    }

    [Fact]
    public async Task Turning_off_show_on_startup_persists()
    {
        var store = new MemoryStore();

        var saved = new AppSettings { ShowHelpOnStartup = false }; // the player unchecked it
        await SettingsService.SaveAsync(store, saved);

        var loaded = new AppSettings();
        await SettingsService.LoadAsync(store, loaded);
        Assert.False(loaded.ShowHelpOnStartup);           // the choice sticks across launches
    }

    /// <summary>An in-memory <see cref="ISaveStore"/> — only the text KV is needed for settings.</summary>
    private sealed class MemoryStore : ISaveStore
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
}
