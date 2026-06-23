using System;
using System.Collections.ObjectModel;
using System.Linq;
using BardsTale.Core.Combat;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Geometry;
using BardsTale.Core.Magic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BardsTale.Desktop.ViewModels;

/// <summary>
/// First-person dungeon exploration: movement, the auto-map, wandering-monster
/// combat and the return trip to town. Wraps a single <see cref="GameState"/>.
/// </summary>
public sealed partial class ExplorationViewModel : ViewModelBase
{
    private readonly GameState _game;

    private readonly RunStats _stats;

    public ExplorationViewModel(GameState game, RunStats? stats = null)
    {
        _game = game;
        _stats = stats ?? new RunStats();
        Party = new ObservableCollection<CharacterViewModel>();
        Log = new ObservableCollection<string>();
        foreach (var m in _game.Party.Members)
            Party.Add(new CharacterViewModel(m));

        AddLog("Your party stands at the dungeon entrance.");
        SyncWorld();
        UpdateLocationState();
    }

    public ObservableCollection<CharacterViewModel> Party { get; }
    public ObservableCollection<string> Log { get; }

    /// <summary>Raised when the party leaves the dungeon to return to Skara Brae.</summary>
    public event Action? ReturnToTownRequested;

    /// <summary>Raised when the party defeats Mangar and wins the game.</summary>
    public event Action? GameWonRequested;

    public Maze Maze => _game.Maze;

    /// <summary>A compact readout of the party's consumables for the exploration HUD.</summary>
    public string InventorySummary
    {
        get
        {
            var parts = _game.Party.Consumables
                .GroupBy(i => i)
                .Select(g => $"{g.Key.Name} x{g.Count()}")
                .ToList();
            var gear = _game.Party.Inventory.Count(i => !i.IsConsumable);
            if (gear > 0) parts.Add($"{gear} gear to equip");
            return parts.Count == 0 ? "Inventory: (empty)" : "Inventory: " + string.Join(", ", parts);
        }
    }

    public bool HasLight => _game.HasLight;
    public string LightText => _game.HasLight ? $"Light: {_game.LightRemaining} steps" : "Light: off";

    [ObservableProperty] private int _partyX;
    [ObservableProperty] private int _partyY;
    [ObservableProperty] private Direction _facing;
    [ObservableProperty] private int _revision;
    [ObservableProperty] private string _locationText = "";
    [ObservableProperty] private bool _canDescend;
    [ObservableProperty] private bool _canAscend;
    [ObservableProperty] private bool _canReturnToTown;

    [ObservableProperty] private bool _isInCombat;
    [ObservableProperty] private CombatViewModel? _combat;

    public bool CanExplore => !IsInCombat;

    [RelayCommand(CanExecute = nameof(CanExplore))]
    private void MoveForward() => Handle(_game.StepForward());

    [RelayCommand(CanExecute = nameof(CanExplore))]
    private void MoveBackward() => Handle(_game.StepBackward());

    [RelayCommand(CanExecute = nameof(CanExplore))]
    private void TurnLeft() => Handle(_game.TurnLeft());

    [RelayCommand(CanExecute = nameof(CanExplore))]
    private void TurnRight() => Handle(_game.TurnRight());

    [RelayCommand(CanExecute = nameof(CanDescend))]
    private void Descend()
    {
        Handle(_game.Descend());
        _stats.DeepestDepth = Math.Max(_stats.DeepestDepth, _game.Depth);
        OnPropertyChanged(nameof(Maze));
    }

    [RelayCommand(CanExecute = nameof(CanAscend))]
    private void Ascend()
    {
        Handle(_game.Ascend());
        OnPropertyChanged(nameof(Maze));
    }

    [RelayCommand(CanExecute = nameof(CanReturnToTown))]
    private void ReturnToTown() => ReturnToTownRequested?.Invoke();

    /// <summary>Conjures light from a Bard's song (free) or a mage's light spell to pierce darkness.</summary>
    [RelayCommand(CanExecute = nameof(CanExplore))]
    private void CastLight()
    {
        if (_game.MagicSuppressed)
        {
            AddLog("Your magic fails in the dead air — no light comes.");
            return;
        }

        var bard = _game.Party.Members.FirstOrDefault(m => !m.IsDead && m.CanSing
            && m.KnownSongs.Select(Songs.Get).Any(s => s.Effect == SongEffect.Light));
        if (bard is not null)
        {
            var song = bard.KnownSongs.Select(Songs.Get).First(s => s.Effect == SongEffect.Light);
            _game.GrantLight(GameState.LightDurationSteps);
            AddLog($"{bard.Name} sings {song.Name}; light fills the passage.");
        }
        else if (_game.Party.Members.FirstOrDefault(m => !m.IsDead && m.KnownSpells.Select(Spells.Get)
                     .Any(s => s.Effect == SpellEffect.RestoreLight && s.Cost <= m.SpellPoints)) is { } caster)
        {
            var spell = caster.KnownSpells.Select(Spells.Get)
                .First(s => s.Effect == SpellEffect.RestoreLight && s.Cost <= caster.SpellPoints);
            caster.SpellPoints -= spell.Cost;
            _game.GrantLight(GameState.LightDurationSteps);
            AddLog($"{caster.Name} casts {spell.Name}; light floods the passage.");
        }
        else
        {
            AddLog("No one can conjure light right now.");
        }
        SyncWorld();
    }

    private void Handle(MoveResult result)
    {
        switch (result.Kind)
        {
            case MoveResultKind.BlockedByWall:
                AddLog(result.Description);
                break;
            case MoveResultKind.Encounter when result.Encounter is not null:
                AddLog(result.Description);
                StartCombat(result.Encounter);
                break;
            case MoveResultKind.Message:
            case MoveResultKind.StairsDown:
            case MoveResultKind.StairsUp:
            case MoveResultKind.Exit:
            case MoveResultKind.Spun:
            case MoveResultKind.Teleported:
            case MoveResultKind.Trapped:
            case MoveResultKind.Darkness:
            case MoveResultKind.AntiMagic:
                AddLog(result.Description);
                break;
        }

        // Poison gnaws with every step taken (a turn or a wall-bump is not a step).
        if (result.Kind is not (MoveResultKind.BlockedByWall or MoveResultKind.Turned or MoveResultKind.Encounter))
            TickPoison();

        UpdateLocationState();
        SyncWorld();
    }

    private void TickPoison()
    {
        foreach (var m in _game.Party.Members)
        {
            if (m.IsDead || !m.IsPoisoned) continue;
            m.ApplyDamage(1);
            AddLog($"{m.Name} takes 1 poison damage.");
            if (m.IsDead)
                AddLog($"{m.Name} has succumbed to poison!");
        }
    }

    private void StartCombat(Encounter encounter)
    {
        var vm = new CombatViewModel(_game.Party, encounter, _game.Rng, _game.MagicSuppressed);
        vm.Finished += OnCombatFinished;
        vm.StateChanged += RefreshParty;
        Combat = vm;
        IsInCombat = true;
        UpdateExploreState();
        vm.Begin();
    }

    private void OnCombatFinished(CombatOutcome outcome, Encounter encounter)
    {
        switch (outcome)
        {
            case CombatOutcome.Victory:
                _stats.BattlesWon++;
                _stats.MonstersSlain += encounter.Groups.Sum(g => g.Monsters.Count);
                _stats.GoldEarned += encounter.TotalGold;
                foreach (var line in _game.ApplyVictory(encounter))
                    AddLog(line);
                if (encounter.IsFinalBoss)
                {
                    _game.ClearBoss();
                    AddLog("MANGAR THE MAD IS DESTROYED! The eternal winter lifts from Skara Brae!");
                    IsInCombat = false;
                    Combat = null;
                    GameWonRequested?.Invoke();
                    return;
                }
                if (encounter.IsBoss)
                {
                    _game.ClearBoss();
                    AddLog("The lair boss is vanquished — the way lies open!");
                }
                AddLog("Victory! The party presses on.");
                break;
            case CombatOutcome.Fled:
                AddLog("You escaped the encounter.");
                break;
            case CombatOutcome.Defeat:
                AddLog("Your party has been wiped out...");
                break;
        }

        IsInCombat = false;
        Combat = null;
        UpdateExploreState();
        SyncWorld();
    }

    private void UpdateLocationState()
    {
        var feature = _game.CurrentCell.Feature;
        var onUpStair = feature == CellFeature.StairsUp;
        CanDescend = feature == CellFeature.StairsDown;
        // An up-stair leads back to town from the entrance level, but climbs a level below it.
        CanAscend = onUpStair && _game.CanAscend;
        CanReturnToTown = onUpStair && !_game.CanAscend;
        DescendCommand.NotifyCanExecuteChanged();
        AscendCommand.NotifyCanExecuteChanged();
        ReturnToTownCommand.NotifyCanExecuteChanged();
    }

    private void UpdateExploreState()
    {
        OnPropertyChanged(nameof(CanExplore));
        MoveForwardCommand.NotifyCanExecuteChanged();
        MoveBackwardCommand.NotifyCanExecuteChanged();
        TurnLeftCommand.NotifyCanExecuteChanged();
        TurnRightCommand.NotifyCanExecuteChanged();
        DescendCommand.NotifyCanExecuteChanged();
        AscendCommand.NotifyCanExecuteChanged();
        ReturnToTownCommand.NotifyCanExecuteChanged();
        CastLightCommand.NotifyCanExecuteChanged();
    }

    private void SyncWorld()
    {
        PartyX = _game.Party.Position.X;
        PartyY = _game.Party.Position.Y;
        Facing = _game.Party.Facing;
        LocationText = $"{_game.Maze.Name}   {_game.Party.Position}   facing {Facing}";
        Revision++;
        OnPropertyChanged(nameof(InventorySummary));
        OnPropertyChanged(nameof(HasLight));
        OnPropertyChanged(nameof(LightText));
        RefreshParty();
    }

    private void RefreshParty()
    {
        foreach (var vm in Party)
            vm.Refresh();
    }

    private void AddLog(string message)
    {
        Log.Add(message);
        while (Log.Count > 100)
            Log.RemoveAt(0);
    }
}
