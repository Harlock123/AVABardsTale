using System.Text.Json;
using System.Threading.Tasks;
using BardsTale.UI.Services;

namespace BardsTale.UI.Settings;

/// <summary>Loads and saves <see cref="AppSettings"/> through the platform's save store.</summary>
public static class SettingsService
{
    private const string Key = "settings";
    // Music fields are nullable so settings files written before music existed load with
    // sensible defaults (music on) rather than a missing-field false/zero.
    private sealed record Dto(bool ReducedMotion, bool Autosave, double UiScale, double SoundVolume, bool Muted,
        bool? MusicEnabled = null, double? MusicVolume = null, bool? CrossfadeMusic = null, bool? IronmanMode = null,
        int? Difficulty = null, bool? ColorblindMode = null,
        int? MoveForwardKey = null, int? MoveBackwardKey = null, int? TurnLeftKey = null, int? TurnRightKey = null,
        bool? ShowHelpOnStartup = null, int? Modifiers = null);

    public static async Task LoadAsync(ISaveStore store, AppSettings into)
    {
        try
        {
            var json = await store.LoadTextAsync(Key);
            if (json is null) return;
            if (JsonSerializer.Deserialize<Dto>(json) is not { } dto) return;
            into.ReducedMotion = dto.ReducedMotion;
            into.Autosave = dto.Autosave;
            into.UiScale = dto.UiScale is >= 0.5 and <= 2.0 ? dto.UiScale : 1.0;
            into.SoundVolume = dto.SoundVolume is >= 0 and <= 1 ? dto.SoundVolume : 0.7;
            into.Muted = dto.Muted;
            into.MusicEnabled = dto.MusicEnabled ?? true;
            into.MusicVolume = dto.MusicVolume is >= 0 and <= 1 ? dto.MusicVolume.Value : 0.20;
            into.CrossfadeMusic = dto.CrossfadeMusic ?? true;
            into.IronmanMode = dto.IronmanMode ?? false;
            into.Difficulty = dto.Difficulty is >= 0 and <= 2 ? (BardsTale.Core.Combat.Difficulty)dto.Difficulty : BardsTale.Core.Combat.Difficulty.Normal;
            into.ColorblindMode = dto.ColorblindMode ?? false;
            if (dto.MoveForwardKey is { } f) into.MoveForwardKey = (Avalonia.Input.Key)f;
            if (dto.MoveBackwardKey is { } b) into.MoveBackwardKey = (Avalonia.Input.Key)b;
            if (dto.TurnLeftKey is { } l) into.TurnLeftKey = (Avalonia.Input.Key)l;
            if (dto.TurnRightKey is { } r) into.TurnRightKey = (Avalonia.Input.Key)r;
            into.ShowHelpOnStartup = dto.ShowHelpOnStartup ?? true;
            var mods = (BardsTale.Core.Game.RunModifier)(dto.Modifiers ?? 0);
            into.ModNoShops = mods.HasFlag(BardsTale.Core.Game.RunModifier.NoShops);
            into.ModRelentless = mods.HasFlag(BardsTale.Core.Game.RunModifier.Relentless);
            into.ModNoCamp = mods.HasFlag(BardsTale.Core.Game.RunModifier.NoCamp);
            into.ModPauper = mods.HasFlag(BardsTale.Core.Game.RunModifier.Pauper);
            into.ModCursed = mods.HasFlag(BardsTale.Core.Game.RunModifier.Cursed);
        }
        catch
        {
            // A missing or corrupt settings blob just means defaults.
        }
    }

    public static Task SaveAsync(ISaveStore store, AppSettings s)
    {
        var json = JsonSerializer.Serialize(new Dto(s.ReducedMotion, s.Autosave, s.UiScale, s.SoundVolume, s.Muted,
            s.MusicEnabled, s.MusicVolume, s.CrossfadeMusic, s.IronmanMode, (int)s.Difficulty, s.ColorblindMode,
            (int)s.MoveForwardKey, (int)s.MoveBackwardKey, (int)s.TurnLeftKey, (int)s.TurnRightKey,
            s.ShowHelpOnStartup, (int)s.SelectedModifiers));
        return store.SaveTextAsync(Key, json);
    }
}
