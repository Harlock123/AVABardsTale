using System;
using System.Collections.ObjectModel;
using System.Linq;
using BardsTale.Core.Game;
using BardsTale.UI.Services;
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
    private readonly SaveService _saves;
    private GameSession _session;

    public MainWindowViewModel() : this(new SaveService()) { }

    public MainWindowViewModel(SaveService saves)
    {
        _saves = saves;
        _session = new GameSession();

        Slots = new ObservableCollection<SaveSlotViewModel>(
            _saves.ManualSlots.Select((s, i) => new SaveSlotViewModel(s, $"Slot {i + 1}", isAutosave: false))
                .Append(new SaveSlotViewModel(SaveService.AutosaveSlot, "Autosave", isAutosave: true)));

        ShowTown();
    }

    [ObservableProperty] private ViewModelBase _currentView = null!;
    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty] private bool _isSlotPanelOpen;
    [ObservableProperty] private SlotPanelMode _slotMode;
    [ObservableProperty] private bool _isGameWon;

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
    private void ShowSaveSlots()
    {
        SlotMode = SlotPanelMode.Save;
        RefreshSlots();
        IsSlotPanelOpen = true;
    }

    [RelayCommand]
    private void ShowLoadSlots()
    {
        SlotMode = SlotPanelMode.Load;
        RefreshSlots();
        IsSlotPanelOpen = true;
    }

    [RelayCommand]
    private void CloseSlots() => IsSlotPanelOpen = false;

    [RelayCommand]
    private void SaveToSlot(SaveSlotViewModel? slot)
    {
        if (slot is null || slot.IsAutosave) return;
        try
        {
            _saves.Save(_session, slot.Slot);
            RefreshSlots();
            StatusMessage = $"Saved to {slot.DisplayName}.";
            IsSlotPanelOpen = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void LoadFromSlot(SaveSlotViewModel? slot)
    {
        if (slot is null || !slot.IsOccupied) return;
        try
        {
            _session = _saves.Load(slot.Slot);
            ShowTown();
            StatusMessage = $"Loaded {slot.DisplayName}.";
            IsSlotPanelOpen = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load failed: {ex.Message}";
        }
    }

    private void RefreshSlots()
    {
        foreach (var slot in Slots)
        {
            slot.IsOccupied = _saves.Exists(slot.Slot);
            slot.Summary = _saves.Describe(slot.Slot);
        }
    }

    private void ShowTown()
    {
        var town = new TownViewModel(_session);
        town.EnterDungeonRequested += OnEnterDungeon;
        CurrentView = town;
    }

    private void OnEnterDungeon()
    {
        var game = _session.EnterDungeon();
        var exploration = new ExplorationViewModel(game, _session.Stats);
        exploration.ReturnToTownRequested += ReturnFromDungeon;
        exploration.GameWonRequested += OnGameWon;
        CurrentView = exploration;
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

        VictoryStats.Clear();
        VictoryStats.Add($"Battles won: {stats.BattlesWon}");
        VictoryStats.Add($"Monsters slain: {stats.MonstersSlain}");
        VictoryStats.Add($"Gold plundered: {stats.GoldEarned}");
        VictoryStats.Add($"Deepest level reached: {stats.DeepestDepth}");

        IsGameWon = true;
        StatusMessage = "Victory! Skara Brae is freed.";
    }

    [RelayCommand]
    private void NewGame()
    {
        _session = new GameSession();
        IsGameWon = false;
        StatusMessage = "A new adventure begins.";
        ShowTown();
    }

    /// <summary>Returning to town is a safe checkpoint, so the game autosaves there.</summary>
    private void ReturnFromDungeon()
    {
        try
        {
            _saves.Save(_session, SaveService.AutosaveSlot);
            StatusMessage = "Returned to town — autosaved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Autosave failed: {ex.Message}";
        }
        ShowTown();
    }
}
