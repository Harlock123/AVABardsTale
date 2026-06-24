using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Geometry;
using BardsTale.Core.Items;
using BardsTale.Core.Magic;
using BardsTale.Core.Town;
using BardsTale.Core.Util;
using BardsTale.UI.Audio;
using CommunityToolkit.Mvvm.ComponentModel;
using ItemDb = BardsTale.Core.Items.Items;
using CommunityToolkit.Mvvm.Input;

namespace BardsTale.UI.ViewModels;

/// <summary>
/// The Skara Brae overworld: the party walks the town square in first person and
/// steps onto buildings to enter them — the Adventurers Guild, Garth's Shoppe, the
/// Temple, the Review Board, the taverns, and the stair down into the catacombs.
/// </summary>
public sealed partial class TownViewModel : ViewModelBase
{
    private readonly GameSession _session;
    private readonly TownMap _town;

    public TownViewModel(GameSession session)
    {
        _session = session;
        _town = session.Town;
        Buildings = _town.Buildings.ToDictionary(b => b.Position, b => b.Building);
        Party = new ObservableCollection<CharacterViewModel>();
        Creation = new CharacterCreationViewModel(session.Factory);
        Stock = new ObservableCollection<ShopItemViewModel>(session.ShopStock.Select(i => new ShopItemViewModel(i)));
        Stash = new ObservableCollection<ShopItemViewModel>();
        TavernRumors = new ObservableCollection<string>();
        SpellMenu = new ObservableCollection<SpellMenuItemViewModel>();
        RebuildParty();
        SyncWorld();
    }

    public ObservableCollection<CharacterViewModel> Party { get; }
    public CharacterCreationViewModel Creation { get; }
    public ObservableCollection<ShopItemViewModel> Stock { get; }

    /// <summary>Unequipped gear (weapons / armour / shields) the party is carrying.</summary>
    public ObservableCollection<ShopItemViewModel> Stash { get; }
    public ObservableCollection<string> TavernRumors { get; }

    /// <summary>Castable restorative spells the party knows, for the between-fights spell menu.</summary>
    public ObservableCollection<SpellMenuItemViewModel> SpellMenu { get; }

    /// <summary>Raised when the party descends from the catacomb stair into the dungeon.</summary>
    public event Action? EnterDungeonRequested;

    // --- Overworld rendering / movement ---
    public Maze Maze => _town.Streets;

    /// <summary>Building entrances by position, so the auto-map can mark each by type.</summary>
    public IReadOnlyDictionary<Position, TownBuilding> Buildings { get; }

    /// <summary>Colour/glyph/label for each building type, for the auto-map legend.</summary>
    public IReadOnlyList<Controls.BuildingMarker> MapLegend => Controls.BuildingMarkers.All;
    [ObservableProperty] private int _partyX;
    [ObservableProperty] private int _partyY;
    [ObservableProperty] private Direction _facing;
    [ObservableProperty] private int _revision;
    [ObservableProperty] private string _bannerText = "";

    // --- Building state ---
    [ObservableProperty] private TownBuilding _activeBuilding = TownBuilding.None;
    [ObservableProperty] private string _currentBuildingName = "";
    [ObservableProperty] private string _notice = "Welcome to Skara Brae. Step onto a building to go inside.";
    [ObservableProperty] private CharacterViewModel? _selectedHero;
    [ObservableProperty] private ShopItemViewModel? _selectedItem;
    [ObservableProperty] private ShopItemViewModel? _selectedStashItem;
    [ObservableProperty] private bool _isSpellMenuOpen;
    [ObservableProperty] private SpellMenuItemViewModel? _selectedSpell;

    public int Gold => _session.Party.Gold;
    public string GoldText => $"{Gold} gold";
    public bool CanEnterDungeon => _session.Party.LivingCount > 0;

    public bool IsInBuilding => ActiveBuilding != TownBuilding.None;
    public bool CanExplore => !IsInBuilding && !IsSpellMenuOpen;
    public bool IsGuild => ActiveBuilding == TownBuilding.Guild;
    public bool IsShop => ActiveBuilding == TownBuilding.Shop;
    public bool IsTemple => ActiveBuilding == TownBuilding.Temple;
    public bool IsReview => ActiveBuilding == TownBuilding.ReviewBoard;
    public bool IsTavern => ActiveBuilding == TownBuilding.Tavern;
    public bool IsInn => ActiveBuilding == TownBuilding.Inn;

    private Position CurrentPosition => new(PartyX, PartyY);
    private BuildingEntrance? BuildingHere => _town.BuildingAt(CurrentPosition);
    public bool CanEnter => CanExplore && BuildingHere is not null;

    partial void OnActiveBuildingChanged(TownBuilding value)
    {
        OnPropertyChanged(nameof(IsInBuilding));
        OnPropertyChanged(nameof(CanExplore));
        OnPropertyChanged(nameof(IsGuild));
        OnPropertyChanged(nameof(IsShop));
        OnPropertyChanged(nameof(IsTemple));
        OnPropertyChanged(nameof(IsReview));
        OnPropertyChanged(nameof(IsTavern));
        OnPropertyChanged(nameof(IsInn));
        OnPropertyChanged(nameof(CanEnter));
        NotifyMovementCanExecute();
    }

    partial void OnIsSpellMenuOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(CanExplore));
        OnPropertyChanged(nameof(CanEnter));
        NotifyMovementCanExecute();
    }

    // --- Movement ---

    [RelayCommand(CanExecute = nameof(CanExplore))]
    private void MoveForward() => Step(Facing);

    [RelayCommand(CanExecute = nameof(CanExplore))]
    private void MoveBackward() => Step(Facing.Opposite());

    [RelayCommand(CanExecute = nameof(CanExplore))]
    private void TurnLeft() { _session.TownFacing = Facing.TurnLeft(); SyncWorld(); }

    [RelayCommand(CanExecute = nameof(CanExplore))]
    private void TurnRight() { _session.TownFacing = Facing.TurnRight(); SyncWorld(); }

    private void Step(Direction dir)
    {
        if (!_town.Streets.CanMove(_session.TownPosition, dir)) return;
        _session.TownPosition = _session.TownPosition.Step(dir);
        SyncWorld();
        Sfx.Play(GameSound.FootstepStone);
    }

    // --- Entering / leaving buildings ---

    [RelayCommand(CanExecute = nameof(CanEnter))]
    private void Enter()
    {
        var entrance = BuildingHere;
        if (entrance is null) return;

        if (entrance.Building == TownBuilding.DungeonEntrance)
        {
            if (!CanEnterDungeon)
            {
                Notice = "You need at least one living adventurer before descending.";
                return;
            }
            EnterDungeonRequested?.Invoke();
            return;
        }

        CurrentBuildingName = entrance.Name;
        if (entrance.Building == TownBuilding.Tavern)
        {
            TavernRumors.Clear();
            Notice = $"You step into {entrance.Name}.";
        }
        if (entrance.Building == TownBuilding.Shop)
            RebuildStash();
        ActiveBuilding = entrance.Building;
        RefreshEconomy();
        Sfx.Play(GameSound.Door);
    }

    [RelayCommand]
    private void Leave() => ActiveBuilding = TownBuilding.None;

    // --- Adventurers Guild ---

    [RelayCommand]
    private void Recruit()
    {
        var candidate = Creation.Candidate;
        if (candidate is null) { Notice = "Roll a character first."; return; }
        if (!_session.JoinParty(candidate))
        {
            Notice = "The party is full (six adventurers maximum).";
            return;
        }
        Notice = $"{candidate.Name} the {candidate.Definition.Name} joins the party!";
        Creation.Reset();
        RebuildParty();
    }

    [RelayCommand]
    private void Dismiss(CharacterViewModel? hero)
    {
        if (hero is null) return;
        _session.Party.Remove(hero.Model);
        Notice = $"{hero.Name} leaves the party.";
        RebuildParty();
    }

    [RelayCommand]
    private void QuickParty()
    {
        _session.FillDefaultParty();
        Notice = "A band of seasoned adventurers joins you.";
        RebuildParty();
    }

    // --- Garth's Equipment Shoppe ---

    [RelayCommand]
    private void Buy()
    {
        var item = SelectedItem;
        if (item is null) { Notice = "Select an item to buy."; return; }
        if (Gold < item.Price) { Notice = $"Not enough gold — {item.Name} costs {item.Price}."; return; }

        // Consumables go straight into the shared stash, no hero required.
        if (item.Item.IsConsumable)
        {
            _session.Party.Gold -= item.Price;
            _session.Party.Inventory.Add(item.Item);
            Notice = $"Bought a {item.Name} for the party stash.";
            Sfx.Play(GameSound.Buy);
            RefreshEconomy();
            return;
        }

        var hero = SelectedHero;
        if (hero is null) { Notice = "Select a hero to equip this."; return; }
        if (!Equipment.CanEquip(hero.Model, item.Item, out var reason)) { Notice = reason; return; }

        _session.Party.Gold -= item.Price;
        var displaced = Equipment.Equip(hero.Model, item.Item);
        if (displaced is not null && displaced.Value > 0)
        {
            var resale = displaced.Value / 2;
            _session.Party.Gold += resale;
            Notice = $"{hero.Name} buys {item.Name}, selling back {displaced.Name} for {resale} gold.";
        }
        else
        {
            Notice = $"{hero.Name} equips {item.Name}.";
        }
        Sfx.Play(GameSound.Buy);
        RefreshEconomy();
    }

    /// <summary>Flat appraisal fee — deliberately uniform so it never hints at the item's power.</summary>
    public int IdentifyCost => 100;

    /// <summary>Garth appraises the selected item for a flat fee.</summary>
    [RelayCommand]
    private void Identify()
    {
        if (!TryBeginIdentify(out var stashed)) return;
        if (Gold < IdentifyCost) { Notice = $"Appraisal costs {IdentifyCost} gold."; return; }
        _session.Party.Gold -= IdentifyCost;
        Reveal(stashed, $"Garth appraises it: a {stashed.Item.Name}!");
    }

    /// <summary>A Rogue appraises the item for free — but may fail and have to try again.</summary>
    [RelayCommand]
    private void IdentifyWithRogue()
    {
        if (!TryBeginIdentify(out var stashed)) return;
        var rogue = _session.Party.Members
            .Where(m => !m.IsDead && m.Class == CharacterClass.Rogue)
            .OrderByDescending(m => m.Level)
            .FirstOrDefault();
        if (rogue is null) { Notice = "No rogue in the party to appraise it."; return; }

        if (_session.Rng.Chance(Identification.RogueChance(rogue.Level)))
            Reveal(stashed, $"{rogue.Name} studies it and recognises a {stashed.Item.Name}!");
        else
            Notice = $"{rogue.Name} turns it over but can't make sense of it. Try again.";
    }

    /// <summary>A caster who knows Scrye Sight identifies the item reliably, for spell points.</summary>
    [RelayCommand]
    private void IdentifyWithMagic()
    {
        if (!TryBeginIdentify(out var stashed)) return;
        var caster = _session.Party.Members.FirstOrDefault(m => !m.IsDead
            && m.KnownSpells.Select(Spells.Get).Any(s => s.Effect == SpellEffect.Identify && s.Cost <= m.SpellPoints));
        if (caster is null) { Notice = "No one has the spell points to cast Scrye Sight."; return; }

        var spell = caster.KnownSpells.Select(Spells.Get)
            .First(s => s.Effect == SpellEffect.Identify && s.Cost <= caster.SpellPoints);
        caster.SpellPoints -= spell.Cost;
        Reveal(stashed, $"{caster.Name} casts {spell.Name}: it is a {stashed.Item.Name}!");
    }

    private bool TryBeginIdentify(out ShopItemViewModel stashed)
    {
        stashed = SelectedStashItem!;
        if (SelectedStashItem is null) { Notice = "Select a stashed item to appraise."; return false; }
        if (SelectedStashItem.Item.Identified) { Notice = $"{SelectedStashItem.Item.Name} is already identified."; return false; }
        return true;
    }

    private void Reveal(ShopItemViewModel stashed, string notice)
    {
        var revealed = stashed.Item.Identify();
        _session.Party.Inventory.Remove(stashed.Item);
        _session.Party.Inventory.Add(revealed);
        RebuildStash();
        SelectedStashItem = Stash.FirstOrDefault(s => s.Item == revealed);
        Notice = notice;
        RefreshEconomy();
    }

    [RelayCommand]
    private void EquipFromStash()
    {
        var hero = SelectedHero;
        var stashed = SelectedStashItem;
        if (hero is null || stashed is null) { Notice = "Select a hero and a stashed item."; return; }
        if (!stashed.Item.Identified) { Notice = "Have it appraised before equipping it."; return; }
        if (!Equipment.CanEquip(hero.Model, stashed.Item, out var reason)) { Notice = reason; return; }

        _session.Party.Inventory.Remove(stashed.Item);
        var displaced = Equipment.Equip(hero.Model, stashed.Item);
        if (displaced is not null && displaced != ItemDb.Fists)
        {
            _session.Party.Inventory.Add(displaced);
            Notice = $"{hero.Name} equips {stashed.Name}, stowing {displaced.Name}.";
        }
        else
        {
            Notice = $"{hero.Name} equips {stashed.Name}.";
        }
        RebuildStash();
        Sfx.Play(GameSound.Equip);
        RefreshEconomy();
    }

    private void RebuildStash()
    {
        Stash.Clear();
        foreach (var item in _session.Party.Inventory.Where(i => !i.IsConsumable))
            Stash.Add(new ShopItemViewModel(item));
        if (SelectedStashItem is not null && !_session.Party.Inventory.Contains(SelectedStashItem.Item))
            SelectedStashItem = null;
    }

    // --- Temple ---

    public int HealCost => _session.Party.Members
        .Where(m => !m.IsDead)
        .Sum(m => (m.MaxHitPoints - m.HitPoints) * 2 + (m.HasAilment ? 50 : 0));

    [RelayCommand]
    private void HealParty()
    {
        var cost = HealCost;
        if (cost == 0) { Notice = "The party is already hale and unafflicted."; return; }
        if (Gold < cost) { Notice = $"Healing the party would cost {cost} gold."; return; }
        _session.Party.Gold -= cost;
        _session.Party.Rest();
        Notice = $"The priests heal the party for {cost} gold.";
        Sfx.Play(GameSound.Heal);
        RefreshEconomy();
    }

    [RelayCommand]
    private void Resurrect(CharacterViewModel? hero)
    {
        if (hero is null || !hero.Model.IsDead) return;
        var cost = 100 * hero.Model.Level;
        if (Gold < cost) { Notice = $"Reviving {hero.Name} costs {cost} gold."; return; }
        _session.Party.Gold -= cost;
        hero.Model.Status &= ~StatusEffect.Dead;
        hero.Model.HitPoints = hero.Model.MaxHitPoints;
        Notice = $"{hero.Name} is restored to life for {cost} gold.";
        Sfx.Play(GameSound.Heal);
        RefreshEconomy();
    }

    [RelayCommand]
    private void RestoreLevels(CharacterViewModel? hero)
    {
        if (hero is null || !hero.Model.IsDrained) return;
        var cost = 200 * hero.Model.DrainedLevels;
        if (Gold < cost) { Notice = $"Restoring {hero.Name}'s lost levels costs {cost} gold."; return; }
        _session.Party.Gold -= cost;
        hero.Model.RestoreLevels();
        Notice = $"The priests restore {hero.Name}'s drained levels for {cost} gold.";
        Sfx.Play(GameSound.Heal);
        RefreshEconomy();
    }

    [RelayCommand]
    private void RestoreStats(CharacterViewModel? hero)
    {
        if (hero is null || !hero.Model.HasDrainedStats) return;
        var cost = 50 * hero.Model.TotalDrainedStats;
        if (Gold < cost) { Notice = $"Restoring {hero.Name}'s withered vigour costs {cost} gold."; return; }
        _session.Party.Gold -= cost;
        hero.Model.RestoreStats();
        Notice = $"The priests restore {hero.Name}'s drained attributes for {cost} gold.";
        Sfx.Play(GameSound.Heal);
        RefreshEconomy();
    }

    // --- Garrick's Inn ---

    /// <summary>A night's lodging — a flat fee per living member that fully restores HP and SP.</summary>
    public int RestCost => 15 * Math.Max(1, _session.Party.LivingCount);

    [RelayCommand]
    private void Rest()
    {
        if (_session.Party.LivingCount == 0) { Notice = "There is no one left to rest."; return; }
        var cost = RestCost;
        if (Gold < cost) { Notice = $"A night's rest costs {cost} gold."; return; }

        _session.Party.Gold -= cost;
        foreach (var m in _session.Party.Members)
            m.FullHeal(); // restores HP and SP for the living; skips the dead
        Notice = $"The party rests the night and wakes fully restored ({cost} gold).";
        Sfx.Play(GameSound.Heal);
        RefreshEconomy();
    }

    // --- Review Board ---

    [RelayCommand]
    private void Advance(CharacterViewModel? hero)
    {
        if (hero is null) return;
        var result = Progression.TryLevelUp(hero.Model, _session.Rng);
        if (result is not null) Sfx.Play(GameSound.LevelUp);
        Notice = result ?? $"{hero.Name} needs more experience to advance.";
        RefreshEconomy();
    }

    // --- Tavern ---

    public int RoundCost => Taverns.RoundCost;

    [RelayCommand]
    private void BuyRound()
    {
        if (Gold < Taverns.RoundCost)
        {
            Notice = $"A round costs {Taverns.RoundCost} gold — your purse is too light.";
            return;
        }
        _session.Party.Gold -= Taverns.RoundCost;
        var rumor = Taverns.RandomRumor(CurrentBuildingName, _session.Rng);
        TavernRumors.Insert(0, rumor);
        Notice = $"You buy a round at {CurrentBuildingName}.";
        Sfx.Play(GameSound.Buy);
        RefreshEconomy();
    }

    // --- Town spell menu ---

    [RelayCommand(CanExecute = nameof(CanExplore))]
    private void OpenSpellMenu()
    {
        BuildSpellMenu();
        Notice = SpellMenu.Count == 0
            ? "No one in the party can cast a restorative spell."
            : "Choose a spell, pick a target hero, and cast.";
        IsSpellMenuOpen = true;
    }

    [RelayCommand]
    private void CloseSpellMenu() => IsSpellMenuOpen = false;

    [RelayCommand]
    private void CastTownSpell()
    {
        if (SelectedSpell is null) { Notice = "Pick a spell to cast."; return; }
        var caster = SelectedSpell.Caster;
        var spell = SelectedSpell.Spell;
        if (caster.SpellPoints < spell.Cost) { Notice = $"{caster.Name} hasn't the spell points."; return; }

        if (!ApplyTownSpell(spell, caster, SelectedHero?.Model, out var message)) { Notice = message; return; }

        caster.SpellPoints -= spell.Cost;
        Sfx.Play(SpellSounds.For(spell.Effect));
        Notice = message;
        RefreshEconomy();
        BuildSpellMenu();
    }

    private void BuildSpellMenu()
    {
        SpellMenu.Clear();
        foreach (var member in _session.Party.Members.Where(m => !m.IsDead))
            foreach (var spell in member.KnownSpells.Select(Spells.Get)
                         .Where(s => s.UsableInTown && s.Cost <= member.SpellPoints))
                SpellMenu.Add(new SpellMenuItemViewModel(member, spell));

        if (SelectedSpell is not null &&
            !SpellMenu.Any(s => s.Caster == SelectedSpell.Caster && s.Spell == SelectedSpell.Spell))
            SelectedSpell = null;
    }

    private bool ApplyTownSpell(Spell spell, Character caster, Character? target, out string message)
    {
        switch (spell.Effect)
        {
            case SpellEffect.HealParty:
                foreach (var m in _session.Party.Members.Where(m => !m.IsDead))
                    m.Heal(spell.Power);
                message = $"{caster.Name} casts {spell.Name}; the party is mended.";
                return true;
            case SpellEffect.HealAlly:
                if (target is null || target.IsDead) { message = "Choose a living ally to heal."; return false; }
                var heal = _session.Rng.Roll(1, spell.Power, spell.Power / 2);
                target.Heal(heal);
                message = $"{caster.Name} casts {spell.Name}, healing {target.Name} for {heal}.";
                return true;
            case SpellEffect.CureStatus:
                if (target is null || target.IsDead) { message = "Choose a living ally to cure."; return false; }
                target.CureAilments();
                message = $"{caster.Name} casts {spell.Name}, cleansing {target.Name}.";
                return true;
            case SpellEffect.Revive:
                if (target is null || !target.IsDead) { message = "Choose a fallen ally to revive."; return false; }
                target.Status &= ~StatusEffect.Dead;
                target.HitPoints = Math.Max(1, spell.Power);
                message = $"{caster.Name} casts {spell.Name}, reviving {target.Name}!";
                return true;
            default:
                message = "That spell cannot be cast here.";
                return false;
        }
    }

    // --- Leave town ---

    [RelayCommand(CanExecute = nameof(CanEnterDungeon))]
    private void EnterDungeon() => EnterDungeonRequested?.Invoke();

    private void NotifyMovementCanExecute()
    {
        MoveForwardCommand.NotifyCanExecuteChanged();
        MoveBackwardCommand.NotifyCanExecuteChanged();
        TurnLeftCommand.NotifyCanExecuteChanged();
        TurnRightCommand.NotifyCanExecuteChanged();
        EnterCommand.NotifyCanExecuteChanged();
        OpenSpellMenuCommand.NotifyCanExecuteChanged();
    }

    private void SyncWorld()
    {
        PartyX = _session.TownPosition.X;
        PartyY = _session.TownPosition.Y;
        Facing = _session.TownFacing;
        Revision++;

        var here = BuildingHere;
        BannerText = here is not null
            ? $"Skara Brae — you stand before {here.Name}  (Enter Building)"
            : "Skara Brae — the town square";

        OnPropertyChanged(nameof(CanEnter));
        NotifyMovementCanExecute();
    }

    private void RebuildParty()
    {
        Party.Clear();
        foreach (var m in _session.Party.Members)
            Party.Add(new CharacterViewModel(m));
        if (SelectedHero is null || !_session.Party.Members.Contains(SelectedHero.Model))
            SelectedHero = Party.FirstOrDefault();
        RefreshEconomy();
    }

    private void RefreshEconomy()
    {
        foreach (var vm in Party)
            vm.Refresh();
        OnPropertyChanged(nameof(Gold));
        OnPropertyChanged(nameof(GoldText));
        OnPropertyChanged(nameof(HealCost));
        OnPropertyChanged(nameof(RestCost));
        OnPropertyChanged(nameof(CanEnterDungeon));
        EnterDungeonCommand.NotifyCanExecuteChanged();
    }
}
