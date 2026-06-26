using System;
using System.Collections.ObjectModel;
using System.Linq;
using BardsTale.Core.Combat;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Geometry;
using BardsTale.Core.Items;
using BardsTale.Core.Lore;
using BardsTale.Core.Magic;
using BardsTale.Core.Quests;
using BardsTale.UI.Audio;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BardsTale.UI.ViewModels;

/// <summary>
/// First-person dungeon exploration: movement, the auto-map, wandering-monster
/// combat and the return trip to town. Wraps a single <see cref="GameState"/>.
/// </summary>
public sealed partial class ExplorationViewModel : ViewModelBase
{
    private readonly GameState _game;

    private readonly RunStats _stats;
    private readonly QuestLog _quests;
    private readonly MonsterCodex _codex;
    private readonly RenownLog _renown;

    public ExplorationViewModel(GameState game, RunStats? stats = null, QuestLog? quests = null,
        MonsterCodex? codex = null, RenownLog? renown = null)
    {
        _game = game;
        _stats = stats ?? new RunStats();
        _quests = quests ?? new QuestLog();
        _codex = codex ?? new MonsterCodex();
        _renown = renown ?? new RenownLog();
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

    /// <summary>Raised when renown changes (an achievement was unlocked), so the shell can refresh.</summary>
    public event Action? RenownChanged;

    private void CheckAchievements()
    {
        var newly = _renown.Sync(_stats, _codex.DiscoveredCount, _quests.Completed.Count);
        if (newly.Count == 0) return;
        foreach (var a in newly)
            AddLog($"🏆 Achievement: {a.Name} (+{a.Renown} renown)");
        RenownChanged?.Invoke();
    }

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
            var gear = _game.Party.Inventory.Count(i => i.Slot is ItemSlot.Weapon or ItemSlot.Armor or ItemSlot.Shield
                or ItemSlot.Ring or ItemSlot.Amulet);
            if (gear > 0) parts.Add($"{gear} gear to equip");
            var embers = _game.Party.Inventory.Count(i => i.Slot == ItemSlot.Material);
            if (embers > 0) parts.Add($"{embers} forge embers");
            return parts.Count == 0 ? "Inventory: (empty)" : "Inventory: " + string.Join(", ", parts);
        }
    }

    public bool HasLight => _game.HasLight;
    public string LightText => _game.HasLight ? $"Light: {_game.LightRemaining} steps" : "Light: off";

    /// <summary>The Camp button label, showing the current ambush risk so the gamble is informed.</summary>
    public string CampText => $"⛺ Camp ({_game.CampAmbushChance * 100:0}% risk)";

    /// <summary>Explains how the camp risk is reduced, for the button tooltip.</summary>
    public string CampTooltip
    {
        get
        {
            var helpers = new System.Collections.Generic.List<string>();
            if (_game.Party.Members.Any(m => !m.IsDead && m.Class == BardsTale.Core.Characters.CharacterClass.Rogue))
                helpers.Add("a Rogue keeps watch");
            if (_game.Party.Members.Any(m => !m.IsDead && m.CanSing))
                helpers.Add("a Bard's song soothes the dark");
            var baseLine = "Rest to recover half the party's HP and spell points — risks a surprise ambush.";
            return helpers.Count > 0 ? $"{baseLine}\nRisk lowered: {string.Join(", ", helpers)}." : baseLine;
        }
    }

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

    /// <summary>True while the party stands at an unopened chest, with the open/leave prompt showing.</summary>
    [ObservableProperty] private bool _isAtChest;
    [ObservableProperty] private string _chestPrompt = "";

    partial void OnIsAtChestChanged(bool value) => UpdateExploreState();

    /// <summary>True while a riddle prompt is showing; the player answers or steps away.</summary>
    [ObservableProperty] private bool _isAtRiddle;
    [ObservableProperty] private string _riddlePrompt = "";
    [ObservableProperty] private string _riddleAnswer = "";

    partial void OnIsAtRiddleChanged(bool value) => UpdateExploreState();

    public bool CanExplore => !IsInCombat && !IsAtChest && !IsAtRiddle;

    [RelayCommand(CanExecute = nameof(CanExplore))]
    private void MoveForward() => Walk(_game.StepForward());

    [RelayCommand(CanExecute = nameof(CanExplore))]
    private void MoveBackward() => Walk(_game.StepBackward());

    private void Walk(MoveResult result)
    {
        Handle(result);
        if (result.Kind == MoveResultKind.Moved) Sfx.Play(GameSound.FootstepDungeon);
    }

    [RelayCommand(CanExecute = nameof(CanExplore))]
    private void TurnLeft() => Handle(_game.TurnLeft());

    [RelayCommand(CanExecute = nameof(CanExplore))]
    private void TurnRight() => Handle(_game.TurnRight());

    [RelayCommand(CanExecute = nameof(CanDescend))]
    private void Descend()
    {
        Handle(_game.Descend());
        _stats.DeepestDepth = Math.Max(_stats.DeepestDepth, _game.Depth);
        CheckAchievements();
        OnPropertyChanged(nameof(Maze));
        Sfx.Play(GameSound.StairsDown);
    }

    [RelayCommand(CanExecute = nameof(CanAscend))]
    private void Ascend()
    {
        Handle(_game.Ascend());
        OnPropertyChanged(nameof(Maze));
        Sfx.Play(GameSound.StairsUp);
    }

    [RelayCommand(CanExecute = nameof(CanReturnToTown))]
    private void ReturnToTown() => ReturnToTownRequested?.Invoke();

    /// <summary>Searches the current cell for hidden doors (a Rogue does it far better).</summary>
    [RelayCommand(CanExecute = nameof(CanExplore))]
    private void Search()
    {
        var result = _game.Search();
        AddLog(result.Message);
        Sfx.Play(result.Found ? GameSound.Door : GameSound.FootstepDungeon);
        SyncWorld(); // an opened door changes the map
    }

    /// <summary>Makes camp to recover HP/SP — at the risk of a wandering ambush that interrupts the rest.</summary>
    [RelayCommand(CanExecute = nameof(CanExplore))]
    private void Camp()
    {
        var result = _game.Camp();
        foreach (var line in result.Log)
            AddLog(line);

        if (result.Ambush is not null)
        {
            Sfx.Play(GameSound.Attack);
            StartCombat(result.Ambush, SurpriseState.PartySurprised);
        }
        else
        {
            Sfx.Play(GameSound.Heal);
        }
        UpdateExploreState();
        SyncWorld();
    }

    /// <summary>Opens the chest underfoot — the Rogue tries the trap, then the party claims the spoils.</summary>
    [RelayCommand]
    private void OpenChest()
    {
        if (!IsAtChest) return;
        var result = _game.OpenChest();
        foreach (var line in result.Log)
            AddLog(line);
        IsAtChest = false;
        ChestPrompt = "";

        // A mimic was lurking — it gets a free swipe, then drop straight into the fight.
        if (result.Mimic is not null)
        {
            AddLog("Pseudopods lash out before anyone can react!");
            Sfx.Play(GameSound.Attack);
            StartCombat(result.Mimic, SurpriseState.PartySurprised);
            UpdateLocationState();
            SyncWorld();
            return;
        }

        Sfx.Play(result.TrapSprang ? GameSound.Hurt : GameSound.Coin);
        UpdateLocationState();
        SyncWorld();
    }

    /// <summary>Walks away from the chest, leaving it latched (it remains for a later visit).</summary>
    [RelayCommand]
    private void LeaveChest()
    {
        AddLog("You leave the chest untouched for now.");
        IsAtChest = false;
        ChestPrompt = "";
    }

    /// <summary>Submits the typed answer to the riddle underfoot.</summary>
    [RelayCommand]
    private void AnswerRiddle()
    {
        if (!IsAtRiddle) return;
        var result = _game.AnswerRiddle(RiddleAnswer);
        foreach (var line in result.Log)
            AddLog(line);

        if (result.Correct)
        {
            Sfx.Play(GameSound.Coin);
            IsAtRiddle = false;
            RiddlePrompt = "";
        }
        else
        {
            Sfx.Play(GameSound.Hurt);
        }
        RiddleAnswer = "";
        SyncWorld();
    }

    /// <summary>Steps away from the riddle, leaving it for later.</summary>
    [RelayCommand]
    private void LeaveRiddle()
    {
        AddLog("You step back from the glowing runes.");
        IsAtRiddle = false;
        RiddlePrompt = "";
        RiddleAnswer = "";
    }

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
            case MoveResultKind.Chest:
                AddLog(result.Description);
                ChestPrompt = result.Description;
                IsAtChest = true;
                break;
            case MoveResultKind.Riddle:
                AddLog(result.Description);
                RiddlePrompt = result.Description;
                RiddleAnswer = "";
                IsAtRiddle = true;
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

    /// <summary>Used by the screenshot runner to stage a battle scene deterministically.</summary>
    internal void StartCombatForScreenshot(Encounter encounter) => StartCombat(encounter);

    private void StartCombat(Encounter encounter, SurpriseState? surprise = null)
    {
        _codex.Discover(encounter, _game.Depth); // the party learns a foe by facing it, win or flee
        Music.Play(GameMusic.Combat);
        var vm = new CombatViewModel(_game.Party, encounter, _game.Rng, _game.MagicSuppressed, surprise);
        vm.Finished += OnCombatFinished;
        vm.StateChanged += OnCombatStateChanged;
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
                _codex.RecordSlain(encounter, _game.Depth);
                foreach (var line in _game.ApplyVictory(encounter))
                    AddLog(line);
                foreach (var line in _quests.RecordVictory(encounter))
                    AddLog(line);
                CheckAchievements();
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
        Music.Play(GameMusic.Dungeon); // fight over — back to the catacomb ambience
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
        CampCommand.NotifyCanExecuteChanged();
        SearchCommand.NotifyCanExecuteChanged();
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
        OnPropertyChanged(nameof(CampText));
        OnPropertyChanged(nameof(CampTooltip));
        RefreshParty();
    }

    /// <summary>After a combat round, pop floating numbers over hit/healed heroes, then refresh.</summary>
    private void OnCombatStateChanged()
    {
        if (Combat is { } combat)
            foreach (var (member, delta) in combat.PartyHpDeltas)
                Party.FirstOrDefault(vm => vm.Model == member)?.Pop(delta);
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
