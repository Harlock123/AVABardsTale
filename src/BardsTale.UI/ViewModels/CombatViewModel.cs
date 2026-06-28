using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Items;
using BardsTale.Core.Magic;
using BardsTale.Core.Util;
using BardsTale.UI.Audio;
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
    private readonly HashSet<Character> _powerUsed = new(); // wielded-item powers fire once per fight
    private readonly HashSet<Character> _abilityUsed = new(); // martial signature abilities fire once per fight
    private int _orderIndex;
    private CombatActionOptionViewModel? _pendingOption;

    public CombatViewModel(Party party, Encounter encounter, IRandomSource rng,
        bool magicSuppressed = false, SurpriseState? surprise = null)
    {
        _party = party;
        _encounter = encounter;
        _magicSuppressed = magicSuppressed;
        _engine = new CombatEngine(party, encounter, rng, magicSuppressed, surprise);

        _partyNames = party.Members.Select(m => m.Name).ToHashSet();

        Groups = new ObservableCollection<MonsterGroupViewModel>(
            encounter.Groups.Select((g, i) => new MonsterGroupViewModel(g, i)));
        Log = new ObservableCollection<CombatLogLineViewModel>();
        AddLog(Intro());
        if (magicSuppressed)
            AddLog("The air is dead to magic here — no spells or songs.");
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
                AddLog("You are ambushed — the enemy strikes before you can react!");
                ResolveRound(new List<CombatCommand>()); // monsters-only opening round
                break;
            case SurpriseState.MonstersSurprised:
                AddLog("You catch them unawares — strike while you can!");
                BeginSelection();
                break;
            default:
                BeginSelection();
                break;
        }
    }

    public ObservableCollection<MonsterGroupViewModel> Groups { get; }
    public ObservableCollection<CombatLogLineViewModel> Log { get; }

    private readonly HashSet<string> _partyNames;

    /// <summary>Adds a combat-log line, classified for colour/icon by the styler.</summary>
    private void AddLog(string text) => Log.Add(new CombatLogLineViewModel(text, _partyNames));
    public ObservableCollection<CombatActionOptionViewModel> Options { get; }
    public ObservableCollection<string> Orders { get; }
    public ObservableCollection<AllyTargetViewModel> AllyTargets { get; }

    [ObservableProperty] private int _selectedGroupIndex;
    [ObservableProperty] private bool _isOver;
    [ObservableProperty] private string _prompt = "";

    /// <summary>True while the player is picking which ally a single-target spell affects.</summary>
    [ObservableProperty] private bool _isChoosingTarget;

    /// <summary>The acting caster's spell points (with a low-SP nudge), shown by the prompt.</summary>
    [ObservableProperty] private string _actorSpText = "";
    [ObservableProperty] private bool _hasActorSp;

    /// <summary>The acting Bard's remaining tunes — songs must be kept up, and new ones cost a tune.</summary>
    [ObservableProperty] private string _actorTunesText = "";
    [ObservableProperty] private bool _hasActorTunes;

    /// <summary>Bumped when the party takes a blow, to shake the combat view (combat juice).</summary>
    [ObservableProperty] private long _shakePulse;

    private void UpdateActorSp(Character actor)
    {
        var combatSpells = actor.KnownSpells.Select(Spells.Get).Where(s => s.UsableInCombat).ToList();
        HasActorSp = actor.IsSpellcaster && combatSpells.Count > 0;
        if (!HasActorSp) { ActorSpText = ""; return; }

        ActorSpText = $"SP {actor.SpellPoints}/{actor.EffectiveMaxSpellPoints}";
        if (!combatSpells.Any(s => s.Cost <= actor.SpellPoints))
            ActorSpText += "  — too low to cast";
    }

    private void UpdateActorTunes(Character actor)
    {
        HasActorTunes = actor.CanSing;
        if (!HasActorTunes) { ActorTunesText = ""; return; }
        ActorTunesText = $"♪ Tunes {actor.BardTunes}/{actor.MaxBardTunes}  — songs sustain while sung; a new tune costs one";
        if (actor.BardTunes == 0) ActorTunesText = $"♪ Tunes 0/{actor.MaxBardTunes}  — spent; can only sustain a current song";
    }

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
        UpdateActorSp(actor);
        UpdateActorTunes(actor);

        Options.Clear();
        var frontRank = _party.FrontRank.ToHashSet();
        // The front rank may swing melee; a back-rank hero needs a ranged weapon to reach the foe.
        if (frontRank.Contains(actor))
            Options.Add(CombatActionOptionViewModel.Attack());
        else if (actor.HasRangedWeapon)
            Options.Add(CombatActionOptionViewModel.Shoot());

        // A martial class's once-per-fight signature manoeuvre (physical — works even in anti-magic).
        var ability = MartialAbilities.For(actor.Class);
        if (ability != MartialAbility.None && !_abilityUsed.Contains(actor)
            && (frontRank.Contains(actor) || MartialAbilities.IsRanged(ability)))
            Options.Add(CombatActionOptionViewModel.MartialAbilityOption(ability));

        if (!_magicSuppressed)
        {
            // Show the whole combat repertoire (affordable first), dimming spells the
            // caster can't currently pay for — parity with the town spell menu.
            foreach (var spell in actor.KnownSpells.Select(Spells.Get)
                         .Where(s => s.UsableInCombat)
                         .OrderByDescending(s => s.Cost <= actor.SpellPoints)
                         .ThenBy(s => s.Level))
                Options.Add(CombatActionOptionViewModel.Cast(spell, actor.SpellPoints));

            if (actor.CanSing)
                foreach (var song in actor.KnownSongs.Select(Songs.Get))
                    Options.Add(CombatActionOptionViewModel.Sing(song));

            // A wielded wand/staff/rod can loose its power once per fight.
            if (actor.Weapon is { HasPower: true } wand && !_powerUsed.Contains(actor))
                Options.Add(CombatActionOptionViewModel.UsePower(wand));
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

        var actor = _actionables[_orderIndex];

        // Reject a spell the caster can't pay for, without spending the turn.
        if (!option.IsItemPower && option.Spell is { } sp && actor.SpellPoints < sp.Cost)
        {
            AddLog($"{actor.Name} hasn't the spell points for {sp.Name}.");
            return;
        }

        // Single-ally spells (heal / cure / revive) drop into a target-picking step.
        if (option.NeedsAllyTarget)
        {
            BeginAllyTargeting(option);
            return;
        }

        var target = ValidTargetIndex();
        var command = option.Action switch
        {
            CombatActionType.Attack => new CombatCommand(actor, CombatActionType.Attack, target),
            CombatActionType.CastSpell => new CombatCommand(actor, CombatActionType.CastSpell, target, Spell: option.Spell),
            CombatActionType.Sing => new CombatCommand(actor, CombatActionType.Sing, Song: option.Song),
            CombatActionType.Ability => new CombatCommand(actor, CombatActionType.Ability, target, Ability: option.Ability),
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
            else if (frontRank.Contains(actor) || actor.HasRangedWeapon)
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

    /// <summary>Per-hero hit-point change from the last resolved round (negative = damage); the
    /// exploration roster reads this to pop floating numbers over party members.</summary>
    public IReadOnlyList<(Character Member, int Delta)> PartyHpDeltas { get; private set; } =
        new List<(Character, int)>();

    private static int GroupHp(MonsterGroup g) => g.Monsters.Sum(m => m.HitPoints);

    private void ResolveRound(List<CombatCommand>? commands = null)
    {
        var resolved = commands ?? _queued;
        PlayActionSounds(resolved);

        // Snapshot hit points so we can pop the per-target deltas as floating numbers.
        var memberHpBefore = _party.Members.ToDictionary(m => m, m => m.HitPoints);
        var groupHpBefore = _encounter.Groups.ToDictionary(g => g, GroupHp);

        var round = _engine.ExecuteRound(resolved);

        // Floating numbers: enemies here, party members via PartyHpDeltas + StateChanged.
        foreach (var gvm in Groups)
            if (groupHpBefore.TryGetValue(gvm.Group, out var before))
                gvm.Pop(GroupHp(gvm.Group) - before);
        PartyHpDeltas = _party.Members
            .Where(m => memberHpBefore.TryGetValue(m, out var b) && m.HitPoints != b)
            .Select(m => (m, m.HitPoints - memberHpBefore[m]))
            .ToList();

        // Combat juice: shake the screen when the party actually took a hit this round.
        if (PartyHpDeltas.Any(d => d.Delta < 0)) ShakePulse++;

        // A character who fired their item power this round can't do so again this fight.
        foreach (var cmd in resolved)
            if (cmd.Action == CombatActionType.CastSpell && cmd.Spell is { } sp
                && cmd.Actor.Weapon is { } w && ReferenceEquals(w.ItemPower, sp))
                _powerUsed.Add(cmd.Actor);
        // ...and a martial signature ability is likewise once per fight.
        foreach (var cmd in resolved)
            if (cmd.Action == CombatActionType.Ability)
                _abilityUsed.Add(cmd.Actor);
        foreach (var line in round.Log)
            AddLog(line);

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

    // Play a sound for each distinct kind of action the party takes this round, so
    // combat is audible whether orders were given by hand or filled by Auto.
    private static void PlayActionSounds(List<CombatCommand> commands)
    {
        var sounds = new HashSet<GameSound>();
        foreach (var cmd in commands)
        {
            switch (cmd.Action)
            {
                case CombatActionType.Attack: sounds.Add(GameSound.Attack); break;
                case CombatActionType.CastSpell when cmd.Spell is { } s: sounds.Add(SpellSounds.For(s.Effect)); break;
                case CombatActionType.Sing: sounds.Add(GameSound.SpellBuff); break;
            }
        }
        foreach (var sound in sounds) Sfx.Play(sound);
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
            CombatActionType.Attack => $"{(option.Label == "Shoot" ? "shoot" : "attack")} {groupName}",
            CombatActionType.Ability => $"{MartialAbilities.Name(option.Ability)} on {groupName}",
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
        if (outcome == CombatOutcome.Victory) Sfx.Play(GameSound.EnemyDefeated);
        else if (outcome == CombatOutcome.Defeat) Sfx.Play(GameSound.Defeat);
        Finished?.Invoke(outcome, _encounter);
    }
}
