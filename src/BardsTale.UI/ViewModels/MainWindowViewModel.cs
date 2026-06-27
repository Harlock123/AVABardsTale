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
        // A failing persist or audio refresh must never crash the app on a mere settings change.
        Settings.PropertyChanged += (_, e) =>
        {
            try
            {
                _ = SettingsService.SaveAsync(_saves, Settings);
                Music.RefreshSettings();
                // The Ironman/difficulty preferences apply to a run that hasn't dived yet (you can
                // still change your mind in town); once in the catacombs they're locked for that run.
                if (e.PropertyName is nameof(AppSettings.IronmanMode) or nameof(AppSettings.Difficulty))
                    ApplyRunPreferences();
            }
            catch { /* a settings side-effect should never bring the game down */ }
        };
        _ = SettingsService.LoadAsync(_saves, Settings).ContinueWith(_ =>
        {
            Music.RefreshSettings();
            ApplyRunPreferences();
            // New players (or anyone who's left it on) get the how-to-play guide on launch.
            if (Settings.ShowHelpOnStartup)
                Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (!IsHelpOpen) ShowHelpCommand.Execute(null); });
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

    // --- Seeded daily challenge ---
    [ObservableProperty] private bool _isChallengePanelOpen;
    [ObservableProperty] private string _challengeSeedInput = "";
    [ObservableProperty] private string _challengeBestText = "";
    /// <summary>On a finished challenge run, the score line shown on the victory / game-over screen.</summary>
    [ObservableProperty] private bool _showChallengeResult;
    [ObservableProperty] private string _challengeResultText = "";

    private static int TodaysSeed => Challenges.DailySeed(System.DateOnly.FromDateTime(System.DateTime.Today));
    public string TodaysSeedText => $"Today's seed: {TodaysSeed}";
    [ObservableProperty] private bool _isSettingsOpen;
    [ObservableProperty] private bool _isQuestLogOpen;
    [ObservableProperty] private QuestLogViewModel? _questLog;
    [ObservableProperty] private bool _isBestiaryOpen;
    [ObservableProperty] private BestiaryViewModel? _bestiary;
    [ObservableProperty] private bool _isAchievementsOpen;
    [ObservableProperty] private AchievementsViewModel? _achievements;
    [ObservableProperty] private bool _isSetCodexOpen;
    [ObservableProperty] private SetCodexViewModel? _setCodex;
    [ObservableProperty] private bool _isHelpOpen;
    [ObservableProperty] private bool _isHistoryOpen;
    [ObservableProperty] private HistoryViewModel? _history;

    /// <summary>The help/tutorial overlay sections — rebuilt on open so the control list reflects current key bindings.</summary>
    public System.Collections.ObjectModel.ObservableCollection<HelpSectionViewModel> HelpSections { get; } = new();

    /// <summary>Top-bar renown badge.</summary>
    public string RenownBadge => $"🏆 {_session.Renown.Renown}";

    private void RefreshRenown() => OnPropertyChanged(nameof(RenownBadge));

    // --- New Game+ / Ironman run badges ---

    /// <summary>True while the active run is Ironman (permadeath); drives the badge and disables manual save/load.</summary>
    public bool IsIronman => _session.Ironman;
    public bool IsNewGamePlus => _session.Ascension > 0;
    public string AscensionBadge => $"NG+{_session.Ascension}";

    /// <summary>A badge for the run's challenge level — shown only when it isn't the default (Normal).</summary>
    public bool ShowDifficultyBadge => _session.Difficulty != BardsTale.Core.Combat.Difficulty.Normal;
    public string DifficultyBadge => _session.Difficulty == BardsTale.Core.Combat.Difficulty.Hard ? "🔥 Hard" : "🌿 Relaxed";

    /// <summary>A badge marking a seeded daily-challenge run, with its seed.</summary>
    public bool IsChallengeRun => _session.IsChallenge;
    public string ChallengeBadge => $"🎯 #{_session.ChallengeSeed}";

    /// <summary>The difficulty options offered by the settings selector.</summary>
    public System.Array DifficultyOptions { get; } = System.Enum.GetValues(typeof(BardsTale.Core.Combat.Difficulty));

    /// <summary>Keys the player may rebind movement to — the letters, minus those reserved for panel hotkeys (J/B/K/Q).</summary>
    public System.Collections.Generic.IReadOnlyList<Avalonia.Input.Key> BindableKeys { get; } = BuildBindableKeys();

    private static System.Collections.Generic.IReadOnlyList<Avalonia.Input.Key> BuildBindableKeys()
    {
        var reserved = new System.Collections.Generic.HashSet<char> { 'J', 'B', 'K', 'Q' };
        var keys = new System.Collections.Generic.List<Avalonia.Input.Key>();
        for (var c = 'A'; c <= 'Z'; c++)
            if (!reserved.Contains(c) && System.Enum.TryParse<Avalonia.Input.Key>(c.ToString(), out var key))
                keys.Add(key);
        return keys;
    }

    /// <summary>Manual save and load are disabled during an Ironman run so death can't be undone.</summary>
    public bool ManualSavesAllowed => !_session.Ironman;

    /// <summary>Applies the Ironman/difficulty preferences to the current run — only before it descends.</summary>
    private void ApplyRunPreferences()
    {
        if (_session.HasActiveDungeon) return; // locked once committed to the catacombs
        _session.Ironman = Settings.IronmanMode;
        _session.Difficulty = Settings.Difficulty;
        RefreshRunBadges();
    }

    private void RefreshRunBadges()
    {
        OnPropertyChanged(nameof(IsIronman));
        OnPropertyChanged(nameof(IsNewGamePlus));
        OnPropertyChanged(nameof(AscensionBadge));
        OnPropertyChanged(nameof(ShowDifficultyBadge));
        OnPropertyChanged(nameof(DifficultyBadge));
        OnPropertyChanged(nameof(IsChallengeRun));
        OnPropertyChanged(nameof(ChallengeBadge));
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

    /// <summary>Opens the help/tutorial overlay, (re)building its sections to reflect the current key bindings.</summary>
    [RelayCommand]
    private void ShowHelp()
    {
        BuildHelpSections();
        IsHelpOpen = true;
    }

    [RelayCommand]
    private void CloseHelp() => IsHelpOpen = false;

    /// <summary>Opens the run-history dashboard, loading the recorded runs from the save store.</summary>
    [RelayCommand]
    private async Task ShowHistory()
    {
        History = new HistoryViewModel(await RunHistory.LoadAsync(_saves));
        IsHistoryOpen = true;
    }

    [RelayCommand]
    private void CloseHistory() => IsHistoryOpen = false;

    /// <summary>F1 / the ❔ button toggles the help overlay.</summary>
    public void ToggleHelp()
    {
        if (IsHelpOpen) CloseHelp();
        else ShowHelp();
    }

    private void BuildHelpSections()
    {
        var s = Settings;
        string Key(Avalonia.Input.Key k) => k.ToString();

        HelpSections.Clear();
        foreach (var section in HelpContent.Build(
            Key(s.MoveForwardKey), Key(s.MoveBackwardKey), Key(s.TurnLeftKey), Key(s.TurnRightKey)))
            HelpSections.Add(section);
    }

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
            ShowChallengeResult = false;
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
        Music.PlayDungeon(game.Depth);
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
        RecordChallengeResult();
        _ = RecordRun("Victory");
    }

    /// <summary>Logs a finished run to the history dashboard (a win, or an Ironman death).</summary>
    private async Task RecordRun(string outcome)
    {
        var stats = _session.Stats;
        var record = new RunRecord(outcome, stats.DeepestDepth, stats.Score, _session.Ironman,
            _session.Ascension, (int)_session.Difficulty, _session.ChallengeSeed ?? -1,
            stats.MonstersSlain, stats.GoldEarned, System.DateTime.Now.Ticks);
        try { await RunHistory.AppendAsync(_saves, record); } catch { /* history is non-essential */ }
    }

    [RelayCommand]
    private void NewGame()
    {
        _session = new GameSession { Ironman = Settings.IronmanMode, Difficulty = Settings.Difficulty };
        IsGameWon = false;
        IsGameOver = false;
        ShowChallengeResult = false;
        StatusMessage = _session.Ironman ? "A new Ironman run begins — there is no second chance." : "A new adventure begins.";
        RefreshRunBadges();
        ShowTown();
    }

    // --- Seeded daily challenge ---

    /// <summary>Opens the daily-challenge panel, defaulting the seed to today's and loading its best.</summary>
    [RelayCommand]
    private async Task ShowChallenge()
    {
        if (string.IsNullOrWhiteSpace(ChallengeSeedInput))
            ChallengeSeedInput = TodaysSeed.ToString();
        OnPropertyChanged(nameof(TodaysSeedText));
        await RefreshChallengeBestAsync();
        IsChallengePanelOpen = true;
    }

    [RelayCommand]
    private void CloseChallenge() => IsChallengePanelOpen = false;

    [RelayCommand]
    private void UseTodaysSeed() => ChallengeSeedInput = TodaysSeed.ToString();

    /// <summary>Re-reads the stored best for the seed in the input box whenever it changes.</summary>
    partial void OnChallengeSeedInputChanged(string value) => _ = RefreshChallengeBestAsync();

    private async Task RefreshChallengeBestAsync()
    {
        if (Challenges.TryParseSeed(ChallengeSeedInput, out var seed))
        {
            var best = await LoadChallengeBestAsync(seed);
            ChallengeBestText = best is { } b ? $"Your best for this seed: {b}" : "No score recorded for this seed yet.";
        }
        else
        {
            ChallengeBestText = "Enter a whole number for the seed.";
        }
    }

    /// <summary>
    /// Starts a seeded challenge run: a fresh session on the seed, the ready-made party, played to
    /// the death (Ironman) on Normal difficulty so every attempt at a seed is judged on equal terms.
    /// </summary>
    [RelayCommand]
    private void StartChallenge()
    {
        if (!Challenges.TryParseSeed(ChallengeSeedInput, out var seed)) return;

        _session = new GameSession(seed)
        {
            ChallengeSeed = seed,
            Ironman = true,
            Difficulty = BardsTale.Core.Combat.Difficulty.Normal
        };
        _session.FillDefaultParty();

        IsChallengePanelOpen = false;
        IsGameWon = false;
        IsGameOver = false;
        ShowChallengeResult = false;
        RefreshRunBadges();
        StatusMessage = $"Daily Challenge #{seed} begins — one party, one life, one seed.";
        ShowTown();
    }

    /// <summary>Records a finished challenge run's score and updates the per-seed best (survives the Ironman save wipe).</summary>
    private async void RecordChallengeResult()
    {
        if (_session.ChallengeSeed is not int seed) return;

        var score = _session.Stats.Score;
        var prevBest = await LoadChallengeBestAsync(seed);
        var isNewBest = prevBest is null || score > prevBest;
        if (isNewBest) await SaveChallengeBestAsync(seed, score);

        ChallengeResultText = isNewBest
            ? $"🎯 Daily Challenge #{seed} — Score {score}   ✦ NEW BEST!"
            : $"🎯 Daily Challenge #{seed} — Score {score}   (your best: {prevBest})";
        ShowChallengeResult = true;
    }

    private static string ChallengeKey(int seed) => $"challenge.best.{seed}";

    private async Task<int?> LoadChallengeBestAsync(int seed)
    {
        try
        {
            var text = await _saves.LoadTextAsync(ChallengeKey(seed));
            return int.TryParse(text, out var best) ? best : null;
        }
        catch { return null; }
    }

    private async Task SaveChallengeBestAsync(int seed, int score)
    {
        try { await _saves.SaveTextAsync(ChallengeKey(seed), score.ToString()); }
        catch { /* best-effort */ }
    }

    /// <summary>After a win, carry the party forward into a tougher New Game+ (Ascension +1).</summary>
    [RelayCommand]
    private void NewGamePlus()
    {
        _session = _session.StartNewGamePlus();
        IsGameWon = false;
        IsGameOver = false;
        ShowChallengeResult = false;
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
        RecordChallengeResult(); // a challenge run is scored even when it ends in death
        await RecordRun("Defeat");
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
