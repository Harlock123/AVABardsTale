using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Items;
using BardsTale.Core.Magic;
using BardsTale.Core.Util;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BardsTale.UI.ViewModels;

/// <summary>
/// Drives combat one round at a time. Each round the player issues an order for
/// every able party member in turn (attack, cast a specific spell, sing, or
/// defend); once everyone has orders the engine resolves the round.
/// </summary>
public sealed partial class CombatViewModel : ViewModelBase
{
    private readonly Party _party;
    private readonly Encounter _encounter;
    private readonly CombatEngine _engine;
    private readonly bool _magicSuppressed;

    private readonly List<Character> _actionables = new();
    private readonly List<CombatCommand> _queued = new();
    private readonly List<Item> _reservedItems = new();
    private int _orderIndex;
    private CombatActionOptionViewModel? _pendingOption;

    public CombatViewModel(Party party, Encounter encounter, IRandomSource rng,
        bool magicSuppressed = false, SurpriseState? surprise = null)
    {
        _party = party;
        _encounter = encounter;
        _magicSuppressed = magicSuppressed;
        _engine = new CombatEngine(party, encounter, rng, magicSuppressed, surprise);

        Groups = new ObservableCollection<MonsterGroupViewModel>(
            encounter.Groups.Select((g, i) => new MonsterGroupViewModel(g, i)));
        Log = new ObservableCollection<string> { Intro() };
        if (magicSuppressed)
            Log.Add("The air is dead to magic here — no spells or songs.");
        Options = new ObservableCollection<CombatActionOptionViewModel>();
        Orders = new ObservableCollection<string>();
        AllyTargets = new ObservableCollection<AllyTargetViewModel>();
    }

    /// <summary>
    /// Starts the fight. Called after the owner has subscribed to events, so a
    /// surprise opening round can resolve (and even conclude) safely.
    /// </summary>
    public void Begin()
    {
        switch (_engine.Surprise)
        {
            case SurpriseState.PartySurprised:
                Log.Add("You are ambushed — the enemy strikes before you can react!");
                ResolveRound(new List<CombatCommand>()); // monsters-only opening round
                break;
            case SurpriseState.MonstersSurprised:
                Log.Add("You catch them unawares — strike while you can!");
                BeginSelection();
                break;
            default:
                BeginSelection();
                break;
        }
    }

    public ObservableCollection<MonsterGroupViewModel> Groups { get; }
    public ObservableCollection<string> Log { get; }
    public ObservableCollection<CombatActionOptionViewModel> Options { get; }
    public ObservableCollection<string> Orders { get; }
    public ObservableCollection<AllyTargetViewModel> AllyTargets { get; }

    [ObservableProperty] private int _selectedGroupIndex;
    [ObservableProperty] private bool _isOver;
    [ObservableProperty] private string _prompt = "";

    /// <summary>True while the player is picking which ally a single-target spell affects.</summary>
    [ObservableProperty] private bool _isChoosingTarget;

    public CombatOutcome Outcome { get; private set; } = CombatOutcome.Ongoing;

    /// <summary>Raised once combat resolves, carrying the final outcome and encounter.</summary>
    public event Action<CombatOutcome, Encounter>? Finished;

    /// <summary>Raised after each round so observers (e.g. the roster) can refresh live.</summary>
    public event Action? StateChanged;

    private string Intro()
    {
        var foes = string.Join(", ", _encounter.Groups.Select(g => $"{g.LivingCount} {g.Name}"));
        return $"You are ambushed by {foes}!";
    }

    // --- Per-character order collection ---

    private void BeginSelection()
    {
        IsChoosingTarget = false;
        _pendingOption = null;
        _actionables.Clear();
        _actionables.AddRange(_party.Members.Where(m => m.CanAct));
        _queued.Clear();
        _reservedItems.Clear();
        Orders.Clear();
        _orderIndex = 0;

        if (_actionables.Count == 0)
        {
            // Nobody can act (asleep/paralysed): the monsters get a free round.
            ResolveRound();
            return;
        }
        ShowCurrentActor();
    }

    private void ShowCurrentActor()
    {
        IsChoosingTarget = false;
        _pendingOption = null;
        var actor = _actionables[_orderIndex];
        Prompt = $"What will {actor.Name} do?  ({_orderIndex + 1}/{_actionables.Count})";

        Options.Clear();
        var frontRank = _party.FrontRank.ToHashSet();
        if (frontRank.Contains(actor))
            Options.Add(CombatActionOptionViewModel.Attack());

        if (!_magicSuppressed)
        {
            foreach (var spell in actor.KnownSpells.Select(Spells.Get)
                         .Where(s => s.UsableInCombat && s.Cost <= actor.SpellPoints)
                         .OrderBy(s => s.Level))
                Options.Add(CombatActionOptionViewModel.Cast(spell));

            if (actor.CanSing)
                foreach (var song in actor.KnownSongs.Select(Songs.Get))
                    Options.Add(CombatActionOptionViewModel.Sing(song));
        }

        foreach (var (item, count) in AvailableConsumables())
            Options.Add(CombatActionOptionViewModel.UseItem(item, count));

        Options.Add(CombatActionOptionViewModel.Defend());
    }

    /// <summary>Consumables still in the stash this round, after orders already queued reserve theirs.</summary>
    private IEnumerable<(Item item, int count)> AvailableConsumables()
        => _party.Consumables
            .GroupBy(i => i)
            .Select(g => (item: g.Key, count: g.Count() - _reservedItems.Count(r => r.Equals(g.Key))))
            .Where(x => x.count > 0);

    [RelayCommand]
    private void ChooseAction(CombatActionOptionViewModel? option)
    {
        if (option is null || IsOver || _orderIndex >= _actionables.Count) return;

        // Single-ally spells (heal / cure / revive) drop into a target-picking step.
        if (option.NeedsAllyTarget)
        {
            BeginAllyTargeting(option);
            return;
        }

        var actor = _actionables[_orderIndex];
        var target = ValidTargetIndex();
        var command = option.Action switch
        {
            CombatActionType.Attack => new CombatCommand(actor, CombatActionType.Attack, target),
            CombatActionType.CastSpell => new CombatCommand(actor, CombatActionType.CastSpell, target, Spell: option.Spell),
            CombatActionType.Sing => new CombatCommand(actor, CombatActionType.Sing, Song: option.Song),
            _ => new CombatCommand(actor, CombatActionType.Defend)
        };
        QueueAndAdvance(command, $"{actor.Name}: {DescribeOrder(option, target)}");
    }

    private void BeginAllyTargeting(CombatActionOptionViewModel option)
    {
        _pendingOption = option;
        var members = _party.Members;

        AllyTargets.Clear();
        for (var i = 0; i < members.Count; i++)
            AllyTargets.Add(new AllyTargetViewModel(members[i], i, IsValidAllyTarget(option, members[i])));

        var what = option.Spell?.Name ?? option.Item?.Name ?? "the action";
        Prompt = $"{_actionables[_orderIndex].Name} — choose a target for {what}";
        IsChoosingTarget = true;
    }

    private static bool IsValidAllyTarget(CombatActionOptionViewModel option, Character m)
    {
        if (option.Spell is { } spell)
            return spell.Effect switch
            {
                SpellEffect.Revive => m.IsDead,
                SpellEffect.CureStatus => !m.IsDead && m.HasAilment,
                _ => !m.IsDead
            };
        if (option.Item is { } item)
            return item.Consumable switch
            {
                ConsumableEffect.Revive => m.IsDead,
                _ => !m.IsDead
            };
        return !m.IsDead;
    }

    [RelayCommand]
    private void ChooseAllyTarget(AllyTargetViewModel? target)
    {
        if (target is null || !target.IsValid || _pendingOption is null) return;

        var actor = _actionables[_orderIndex];
        CombatCommand command;
        string description;

        if (_pendingOption.Item is { } item)
        {
            command = new CombatCommand(actor, CombatActionType.UseItem, Item: item, TargetAllyIndex: target.Index);
            description = $"{actor.Name}: use {item.Name} on {target.Name}";
            _reservedItems.Add(item);
        }
        else
        {
            var spell = _pendingOption.Spell!;
            command = new CombatCommand(actor, CombatActionType.CastSpell, ValidTargetIndex(),
                Spell: spell, TargetAllyIndex: target.Index);
            description = $"{actor.Name}: cast {spell.Name} on {target.Name}";
        }

        IsChoosingTarget = false;
        _pendingOption = null;
        QueueAndAdvance(command, description);
    }

    [RelayCommand]
    private void CancelTarget()
    {
        if (!IsChoosingTarget) return;
        ShowCurrentActor();
    }

    private void QueueAndAdvance(CombatCommand command, string description)
    {
        _queued.Add(command);
        Orders.Add(description);
        _orderIndex++;

        if (_orderIndex >= _actionables.Count)
            ResolveRound();
        else
            ShowCurrentActor();
    }

    [RelayCommand]
    private void UndoLast()
    {
        if (IsOver || _orderIndex == 0) return;
        _orderIndex--;
        var removed = _queued[^1];
        _queued.RemoveAt(_queued.Count - 1);
        Orders.RemoveAt(Orders.Count - 1);
        if (removed.Action == CombatActionType.UseItem && removed.Item is { } item)
            _reservedItems.Remove(item);
        ShowCurrentActor();
    }

    /// <summary>Fills any remaining orders with sensible defaults and resolves the round.</summary>
    [RelayCommand]
    private void Auto()
    {
        if (IsOver) return;
        var target = ValidTargetIndex();
        var frontRank = _party.FrontRank.ToHashSet();

        while (_orderIndex < _actionables.Count)
        {
            var actor = _actionables[_orderIndex];
            var damage = _magicSuppressed ? null : actor.KnownSpells.Select(Spells.Get)
                .Where(s => s.TargetsEnemies && s.Cost <= actor.SpellPoints)
                .OrderByDescending(s => s.Power).FirstOrDefault();

            CombatCommand cmd;
            if (damage is not null && actor.SpellPoints >= damage.Cost)
                cmd = new CombatCommand(actor, CombatActionType.CastSpell, target, Spell: damage);
            else if (frontRank.Contains(actor))
                cmd = new CombatCommand(actor, CombatActionType.Attack, target);
            else
                cmd = new CombatCommand(actor, CombatActionType.Defend);

            _queued.Add(cmd);
            _orderIndex++;
        }
        ResolveRound();
    }

    [RelayCommand]
    private void Flee()
    {
        if (IsOver) return;
        ResolveRound(_party.Members.Where(m => m.CanAct)
            .Select(m => new CombatCommand(m, CombatActionType.Flee)).ToList());
    }

    private void ResolveRound(List<CombatCommand>? commands = null)
    {
        var round = _engine.ExecuteRound(commands ?? _queued);
        foreach (var line in round.Log)
            Log.Add(line);

        // Show any reinforcements that were summoned into the fight this round.
        while (Groups.Count < _encounter.Groups.Count)
            Groups.Add(new MonsterGroupViewModel(_encounter.Groups[Groups.Count], Groups.Count));

        foreach (var g in Groups) g.Refresh();
        StateChanged?.Invoke();

        if (round.Outcome != CombatOutcome.Ongoing)
        {
            Conclude(round.Outcome);
            return;
        }
        BeginSelection();
    }

    // --- Targeting helpers ---

    private int ValidTargetIndex()
    {
        if (SelectedGroupIndex >= 0 && SelectedGroupIndex < _encounter.Groups.Count
            && !_encounter.Groups[SelectedGroupIndex].IsDefeated)
            return SelectedGroupIndex;
        return Groups.FirstOrDefault(g => !g.IsDefeated)?.Index ?? 0;
    }

    private string DescribeOrder(CombatActionOptionViewModel option, int target)
    {
        var groupName = target < Groups.Count ? Groups[target].Name : "enemies";
        return option.Action switch
        {
            CombatActionType.Attack => $"attack {groupName}",
            CombatActionType.CastSpell when option.Spell!.TargetsEnemies => $"cast {option.Spell.Name} at {groupName}",
            CombatActionType.CastSpell => $"cast {option.Spell!.Name}",
            CombatActionType.Sing => $"sing {option.Song!.Name}",
            _ => "defend"
        };
    }

    private void Conclude(CombatOutcome outcome)
    {
        Outcome = outcome;
        IsOver = true;
        Options.Clear();
        Prompt = outcome switch
        {
            CombatOutcome.Victory => "The enemies are defeated!",
            CombatOutcome.Fled => "You slip away from the fight.",
            CombatOutcome.Defeat => "The party has fallen...",
            _ => ""
        };
        Finished?.Invoke(outcome, _encounter);
    }
}
