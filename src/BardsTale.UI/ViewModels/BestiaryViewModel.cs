using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Media;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Lore;

namespace BardsTale.UI.ViewModels;

/// <summary>
/// The bestiary overlay: the full monster roster in catalogue order, each entry
/// either filled in (if the party has faced it) or shown as an undiscovered mystery
/// to hint at what's left to find.
/// </summary>
public sealed class BestiaryViewModel : ViewModelBase
{
    public BestiaryViewModel(MonsterCodex codex)
    {
        DiscoveredCount = codex.DiscoveredCount;
        TotalCount = codex.TotalCount;
        Entries = new ObservableCollection<BestiaryEntryViewModel>();
        foreach (var template in MonsterCatalog.All)
            Entries.Add(new BestiaryEntryViewModel(template, codex.For(template.Name)));
    }

    public ObservableCollection<BestiaryEntryViewModel> Entries { get; }

    public int DiscoveredCount { get; }
    public int TotalCount { get; }
    public string Heading => $"Bestiary — {DiscoveredCount} of {TotalCount} discovered";
}

/// <summary>One monster's bestiary card — full stats once discovered, a mystery until then.</summary>
public sealed class BestiaryEntryViewModel : ViewModelBase
{
    private readonly MonsterTemplate _t;
    private readonly CodexEntry? _entry;

    public BestiaryEntryViewModel(MonsterTemplate template, CodexEntry? entry)
    {
        _t = template;
        _entry = entry;
    }

    public bool IsDiscovered => _entry is not null;

    public string Name => IsDiscovered ? _t.Name : "??? — undiscovered";

    public string StatLine => IsDiscovered
        ? $"HP {_t.MaxHitPoints}    AC {_t.ArmorClass}    Dmg {Damage}    Speed {_t.Speed}"
        : "An unknown terror still lurking somewhere in the catacombs.";

    public string Worth => IsDiscovered ? $"Worth {_t.ExperienceValue} XP · {_t.GoldValue} gold" : "";

    public string Notes => IsDiscovered ? BuildNotes() : "";
    public bool HasNotes => Notes.Length > 0;

    public string TallyLine => _entry is null
        ? ""
        : $"Slain: {_entry.Slain}    ·    First met on level {_entry.FirstSeenDepth}";

    public IBrush NameBrush => IsDiscovered ? Gold : Slate;

    private string Damage
    {
        get
        {
            var min = _t.AttackDice + _t.AttackBonus;
            var max = _t.AttackDice * _t.AttackSides + _t.AttackBonus;
            var dice = $"{_t.AttackDice}d{_t.AttackSides}{(_t.AttackBonus > 0 ? $"+{_t.AttackBonus}" : "")}";
            return $"{dice} ({min}–{max})";
        }
    }

    private string BuildNotes()
    {
        var parts = new List<string>();
        switch (_t.InflictsStatus)
        {
            case StatusEffect.Poisoned: parts.Add("its bite inflicts poison"); break;
            case StatusEffect.Asleep: parts.Add("its touch lulls foes to sleep"); break;
            case StatusEffect.Paralyzed: parts.Add("its hit can paralyse"); break;
        }
        switch (_t.Ability)
        {
            case MonsterAbility.DrainLevel: parts.Add("it drains experience levels"); break;
            case MonsterAbility.DrainStat: parts.Add("it withers attributes"); break;
            case MonsterAbility.StealGold: parts.Add("it steals gold from the purse"); break;
        }
        if (_t.Spell is { } spell)
            parts.Add($"it casts {spell.Name}");
        if (parts.Count == 0) return "";

        var joined = string.Join("; ", parts);
        return char.ToUpper(joined[0]) + joined[1..] + ".";
    }

    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#E8C56B"));
    private static readonly IBrush Slate = new SolidColorBrush(Color.Parse("#6B7280"));
}
