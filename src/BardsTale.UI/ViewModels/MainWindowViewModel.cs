using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BardsTale.Core.Game;
using BardsTale.Core.Items;
using BardsTale.UI.Audio;
using BardsTale.UI.Services;
using BardsTale.UI.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BardsTale.UI.ViewModels;

public enum SlotPanelMode { Save, Load }

/// <summary>
/// Root navigator. Owns the <see cref="GameSession"/> and swaps the active screen
/// between the town hub and dungeon exploration. Also drives the multi-slot save
/// system, an autosave on returning to town, and the slot-picker overlay.
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly ISaveStore _saves;
    private GameSession _session;

    public MainWindowViewModel() : this(App.SaveStoreFactory()) { }

    public MainWindowViewModel(ISaveStore saves)
    {
        _saves = saves;
        _session = new GameSession();

        Slots = new ObservableCollection<SaveSlotViewModel>(
            _saves.ManualSlots.Select((s, i) => new SaveSlotViewModel(s, $"Slot {i + 1}", isAutosave: false))
                .Append(new SaveSlotViewModel(SaveSlots.Autosave, "Autosave", isAutosave: true)));

        // Persist preference changes and re-apply music settings; then load saved settings.
        Settings.PropertyChanged += (_, e) =>
        {
            _ = SettingsService.SaveAsync(_saves, Settings);
            Music.RefreshSettings();
            // The Ironman preference applies to a run that hasn't dived yet (you can still change
            // your mind in town); once committed to the catacombs it's locked for that run.
            if (e.PropertyName == nameof(AppSettings.IronmanMode) && !_session.HasActiveDungeon)
            {
                _session.Ironman = Settings.IronmanMode;
                RefreshRunBadges();
            }
        };
        _ = SettingsService.LoadAsync(_saves, Settings).ContinueWith(_ =>
        {
            Music.RefreshSettings();
            if (!_session.HasActiveDungeon) { _session.Ironman = Settings.IronmanMode; RefreshRunBadges(); }
        });

        ShowTown();
    }

    /// <summary>Shared user preferences, bound by the settings screen.</summary>
    public AppSettings Settings => AppSettings.Current;

    [ObservableProperty] private ViewModelBase _currentView = null!;
    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty] private bool _isSlotPanelOpen;
    [ObservableProperty] private SlotPanelMode _slotMode;
    [ObservableProperty] private bool _isGameWon;
    [ObservableProperty] private bool _isGameOver;
    [ObservableProperty] private bool _isSettingsOpen;
    [ObservableProperty] private bool _isQuestLogOpen;
    [ObservableProperty] private QuestLogViewModel? _questLog;
    [ObservableProperty] private bool _isBestiaryOpen;
    [ObservableProperty] private BestiaryViewModel? _bestiary;
    [ObservableProperty] private bool _isAchievementsOpen;
    [ObservableProperty] private AchievementsViewModel? _achievements;
    [ObservableProperty] private bool _isSetCodexOpen;
    [ObservableProperty] private SetCodexViewModel? _setCodex;

    /// <summary>Top-bar renown badge.</summary>
    public string RenownBadge => $"🏆 {_session.Renown.Renown}";

    private void RefreshRenown() => OnPropertyChanged(nameof(RenownBadge));

    // --- New Game+ / Ironman run badges ---

    /// <summary>True while the active run is Ironman (permadeath); drives the badge and disables manual save/load.</summary>
    public bool IsIronman => _session.Ironman;
    public bool IsNewGamePlus => _session.Ascension > 0;
    public string AscensionBadge => $"NG+{_session.Ascension}";

    /// <summary>Manual save and load are disabled during an Ironman run so death can't be undone.</summary>
    public bool ManualSavesAllowed => !_session.Ironman;

    private void RefreshRunBadges()
    {
        OnPropertyChanged(nameof(IsIronman));
        OnPropertyChanged(nameof(IsNewGamePlus));
        OnPropertyChanged(nameof(AscensionBadge));
        OnPropertyChanged(nameof(ManualSavesAllowed));
    }

    public ObservableCollection<SaveSlotViewModel> Slots { get; }

    public bool IsSaveMode => SlotMode == SlotPanelMode.Save;
    public bool IsLoadMode => SlotMode == SlotPanelMode.Load;
    public string SlotPanelTitle => IsSaveMode ? "Save to which slot?" : "Load which slot?";

    partial void OnSlotModeChanged(SlotPanelMode value)
    {
        OnPropertyChanged(nameof(IsSaveMode));
        OnPropertyChanged(nameof(IsLoadMode));
        OnPropertyChanged(nameof(SlotPanelTitle));
    }

    /// <summary>The active exploration screen, if any — used to route keyboard input.</summary>
    public ExplorationViewModel? Exploration => CurrentView as ExplorationViewModel;

    /// <summary>The active town screen, if any — used to route keyboard input.</summary>
    public TownViewModel? Town => CurrentView as TownViewModel;

    [RelayCommand]
    private async Task ShowSaveSlots()
    {
        if (!ManualSavesAllowed) { StatusMessage = "Ironman: no manual saves — your fate is sealed by the autosave alone."; return; }
        SlotMode = SlotPanelMode.Save;
        await RefreshSlotsAsync();
        IsSlotPanelOpen = true;
    }

    [RelayCommand]
    private async Task ShowLoadSlots()
    {
        if (!ManualSavesAllowed) { StatusMessage = "Ironman: there is no reloading — press on or perish."; return; }
        SlotMode = SlotPanelMode.Load;
        await RefreshSlotsAsync();
        IsSlotPanelOpen = true;
    }

    [RelayCommand]
    private void CloseSlots() => IsSlotPanelOpen = false;

    [RelayCommand]
    private void ShowSettings() => IsSettingsOpen = true;

    [RelayCommand]
    private void CloseSettings() => IsSettingsOpen = false;

    /// <summary>Opens the quest journal, rebuilding it from the current quest progress.</summary>
    [RelayCommand]
    private void ShowQuestLog()
    {
        QuestLog = new QuestLogViewModel(_session.Quests);
        IsQuestLogOpen = true;
    }

    [RelayCommand]
    private void CloseQuestLog() => IsQuestLogOpen = false;

    /// <summary>Gives up a quest, forfeiting its progress, and drops it from the journal.</summary>
    [RelayCommand]
    private void AbandonQuest(QuestEntryViewModel? entry)
    {
        if (entry is null) return;
        _session.Quests.Abandon(entry.Model);
        QuestLog?.Remove(entry);
        StatusMessage = $"Abandoned quest: \"{entry.Model.Title}\".";
    }

    /// <summary>The 'J' key toggles the journal open and shut.</summary>
    public void ToggleQuestLog()
    {
        if (IsQuestLogOpen) CloseQuestLog();
        else ShowQuestLog();
    }

    /// <summary>Opens the bestiary, rebuilding it from what the party has discovered.</summary>
    [RelayCommand]
    private void ShowBestiary()
    {
        Bestiary = new BestiaryViewModel(_session.Codex);
        IsBestiaryOpen = true;
    }

    [RelayCommand]
    private void CloseBestiary() => IsBestiaryOpen = false;

    /// <summary>The 'B' key toggles the bestiary.</summary>
    public void ToggleBestiary()
    {
        if (IsBestiaryOpen) CloseBestiary();
        else ShowBestiary();
    }

    /// <summary>Opens the accessory-set codex, rebuilt from the party's current gear.</summary>
    [RelayCommand]
    private void ShowSetCodex()
    {
        SetCodex = new SetCodexViewModel(_session.Party);
        IsSetCodexOpen = true;
    }

    [RelayCommand]
    private void CloseSetCodex() => IsSetCodexOpen = false;

    /// <summary>The 'K' key toggles the set codex.</summary>
    public void ToggleSetCodex()
    {
        if (IsSetCodexOpen) CloseSetCodex();
        else ShowSetCodex();
    }

    /// <summary>Opens the achievements & renown overlay (rebuilt from current standing).</summary>
    [RelayCommand]
    private void ShowAchievements()
    {
        Achievements = new AchievementsViewModel(_session.Renown);
        IsAchievementsOpen = true;
    }

    [RelayCommand]
    private void CloseAchievements() => IsAchievementsOpen = false;

    [RelayCommand]
    private async Task SaveToSlot(SaveSlotViewModel? slot)
    {
        if (slot is null || slot.IsAutosave) return;
        try
        {
            await _saves.SaveAsync(_session, slot.Slot);
            await RefreshSlotsAsync();
            StatusMessage = $"Saved to {slot.DisplayName}.";
            IsSlotPanelOpen = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadFromSlot(SaveSlotViewModel? slot)
    {
        if (slot is null || !slot.IsOccupied) return;
        try
        {
            _session = await _saves.LoadAsync(slot.Slot);
            IsGameWon = false;
            IsGameOver = false;
            RefreshRunBadges();
            ShowTown();
            StatusMessage = $"Loaded {slot.DisplayName}.";
            IsSlotPanelOpen = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load failed: {ex.Message}";
        }
    }

    private async Task RefreshSlotsAsync()
    {
        foreach (var slot in Slots)
        {
            slot.IsOccupied = await _saves.ExistsAsync(slot.Slot);
            slot.Summary = await _saves.DescribeAsync(slot.Slot);
        }
    }

    // --- Hooks used by the screenshot runner to drive navigation (see ScreenshotRunner) ---
    internal GameSession Session => _session;
    internal void ShowTownScreen() => ShowTown();
    internal void EnterDungeonScreen() => OnEnterDungeon();

    private void ShowTown()
    {
        var town = new TownViewModel(_session);
        town.EnterDungeonRequested += OnEnterDungeon;
        town.RenownChanged += RefreshRenown;
        CurrentView = town;
        Music.Play(GameMusic.Town);
        RefreshRenown();
    }

    // The deepest floor reached before the current dive, to spot newly-unlocked Garth's wares.
    private int _deepestBeforeDive = 1;

    private void OnEnterDungeon()
    {
        _deepestBeforeDive = _session.Stats.DeepestDepth;
        var game = _session.EnterDungeon();
        var exploration = new ExplorationViewModel(game, _session.Stats, _session.Quests, _session.Codex, _session.Renown);
        exploration.ReturnToTownRequested += ReturnFromDungeon;
        exploration.GameWonRequested += OnGameWon;
        exploration.PartyWipedRequested += OnPartyWiped;
        exploration.RenownChanged += RefreshRenown;
        CurrentView = exploration;
        Music.Play(GameMusic.Dungeon);
        RefreshRenown();
    }

    public ObservableCollection<string> VictoryParty { get; } = new();
    public ObservableCollection<string> VictoryStats { get; } = new();

    private void OnGameWon()
    {
        var stats = _session.Stats;

        VictoryParty.Clear();
        foreach (var m in _session.Party.Members)
            VictoryParty.Add($"{m.Name} — {BardsTale.Core.Characters.Races.Get(m.Race).Name} "
                + $"{m.Definition.Name}, level {m.Level}{(m.IsDead ? " (fallen)" : "")}");

        stats.Victory = true;
        _session.SyncAchievements();
        RefreshRenown();

        VictoryStats.Clear();
        VictoryStats.Add($"Battles won: {stats.BattlesWon}");
        VictoryStats.Add($"Monsters slain: {stats.MonstersSlain}");
        VictoryStats.Add($"Gold plundered: {stats.GoldEarned}");
        VictoryStats.Add($"Deepest level reached: {stats.DeepestDepth}");
        VictoryStats.Add($"Renown earned: {_session.Renown.Renown}");

        IsGameWon = true;
        StatusMessage = "Victory! Skara Brae is freed.";
        Sfx.Play(GameSound.Victory);
        Music.Play(GameMusic.Victory);
    }

    [RelayCommand]
    private void NewGame()
    {
        _session = new GameSession { Ironman = Settings.IronmanMode };
        IsGameWon = false;
        IsGameOver = false;
        StatusMessage = _session.Ironman ? "A new Ironman run begins — there is no second chance." : "A new adventure begins.";
        RefreshRunBadges();
        ShowTown();
    }

    /// <summary>After a win, carry the party forward into a tougher New Game+ (Ascension +1).</summary>
    [RelayCommand]
    private void NewGamePlus()
    {
        _session = _session.StartNewGamePlus();
        IsGameWon = false;
        IsGameOver = false;
        StatusMessage = $"New Game+ begins — Ascension {_session.Ascension}. The catacombs grow deadlier, but your heroes carry on.";
        RefreshRunBadges();
        ShowTown();
    }

    /// <summary>
    /// A total party kill. In an Ironman run this is permanent: the saves are wiped and a
    /// game-over banner shows. (Outside Ironman the party can still be carried back and revived.)
    /// </summary>
    private async void OnPartyWiped()
    {
        if (!_session.Ironman) return;

        try
        {
            await _saves.DeleteAsync(SaveSlots.Autosave);
            foreach (var slot in _saves.ManualSlots) await _saves.DeleteAsync(slot);
        }
        catch { /* best-effort wipe */ }

        Sfx.Play(GameSound.Hurt);
        Music.Stop();
        StatusMessage = "The Ironman run ends here.";
        IsGameOver = true;
    }

    /// <summary>Returning to town is a safe checkpoint, so the game autosaves there (if enabled).</summary>
    private async void ReturnFromDungeon()
    {
        // Fresh notices go up on the board while the party was away.
        _session.QuestBoard.Restock(_session.Rng, System.Math.Max(1, _session.Stats.DeepestDepth));

        // Reaching a new depth this dive may have expanded Garth's stock — call it out.
        var crossed = ShopWares.NewUnlocksBetween(_deepestBeforeDive, _session.Stats.DeepestDepth);

        if (Settings.Autosave)
        {
            try
            {
                await _saves.SaveAsync(_session, SaveSlots.Autosave);
                StatusMessage = "Returned to town — autosaved.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Autosave failed: {ex.Message}";
            }
        }

        if (crossed.Count > 0)
        {
            StatusMessage = $"Garth's Equipment Shoppe has restocked — new wares for reaching floor {crossed.Max()}!";
            Sfx.Play(GameSound.Coin);
        }
        ShowTown();
    }
}
