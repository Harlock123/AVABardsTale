using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Magic;
using Attribute = BardsTale.Core.Characters.Attribute;

namespace BardsTale.UI.ViewModels;

/// <summary>
/// The character-sheet overlay: a full inspectable readout of every hero's numbers —
/// attributes, derived bonuses, attacks per round, effective HP/SP, wards, set bonuses,
/// gear and known magic — so the depth baked into the model is actually visible.
/// </summary>
public sealed class PartySheetViewModel
{
    public PartySheetViewModel(Party party)
    {
        Heroes = party.Members.Select(m => new CharacterSheetViewModel(m, party.Boon)).ToList();
        BoonLine = party.Boon == PartyBoon.None
            ? "No shrine boon active."
            : $"Shrine boon — {Boons.Label(party.Boon)}: {Boons.Describe(party.Boon)}.";
        HasBoon = party.Boon != PartyBoon.None;
    }

    public string Heading => "Party Roster";
    public IReadOnlyList<CharacterSheetViewModel> Heroes { get; }
    public string BoonLine { get; }
    public bool HasBoon { get; }
}

/// <summary>One hero's full stat card for the inspect overlay.</summary>
public sealed class CharacterSheetViewModel
{
    private readonly Character _c;

    public CharacterSheetViewModel(Character c, PartyBoon boon)
    {
        _c = c;
        _boon = boon;
    }

    private readonly PartyBoon _boon;

    public string Header => $"{_c.Name} — {Races.Get(_c.Race).Name} {_c.Definition.Name}";
    public string LevelLine => $"Level {_c.Level}    ·    XP {_c.Experience:N0} / {_c.ExperienceForNextLevel:N0}"
        + (Progression.CanLevelUp(_c) ? "    ·    ready to advance!" : "");

    public string Vitals
    {
        get
        {
            var sp = _c.IsSpellcaster ? $"    SP {_c.SpellPoints}/{_c.EffectiveMaxSpellPoints}" : "";
            return $"HP {_c.HitPoints}/{_c.EffectiveMaxHitPoints}{sp}    AC {_c.ArmorClass}    Attacks/round {_c.AttacksPerRound}";
        }
    }

    /// <summary>The five attributes, each a chip; the class's prime attribute is highlighted.</summary>
    public IReadOnlyList<AttributeChipViewModel> Attributes
    {
        get
        {
            var prime = Progression.PrimeAttribute(_c.Class);
            return System.Enum.GetValues<Attribute>()
                .Select(a => new AttributeChipViewModel(
                    $"{AttributeSet.Abbreviation(a)} {_c.Attributes[a]}", a == prime))
                .ToList();
        }
    }

    /// <summary>Derived numbers the attributes and gear actually feed into.</summary>
    public string Derived
    {
        get
        {
            var lines = new List<string>
            {
                $"Melee damage bonus  {Signed(_c.StrengthBonus)}  (Strength)",
                $"Dexterity bonus  {Signed(_c.DexterityBonus)}  (sharpens armour class)",
                $"Effective luck  {_c.EffectiveLuck}",
            };
            if (_c.GearHitBonus != 0 || _c.GearDamageBonus != 0)
                lines.Add($"Gear  {Signed(_c.GearHitBonus)} to-hit,  {Signed(_c.GearDamageBonus)} damage");
            if (_c.RegenPerRound > 0)
                lines.Add($"Regeneration  +{_c.RegenPerRound} HP each round");
            return string.Join("\n", lines);
        }
    }

    public bool HasWards => _c.ResistedElements != Element.None;
    public string Wards => HasWards
        ? $"Wards  {MonsterElements.DescribeGlyphs(_c.ResistedElements)}  ({MonsterElements.Describe(_c.ResistedElements)})"
        : "Wards  none";

    public bool HasSets => _c.ActiveSets.Count > 0;
    public string Sets => HasSets
        ? "Set bonuses  " + string.Join(";  ", _c.ActiveSets.Select(s => $"{s.Name} ({s.BonusSummary})"))
        : "";

    public string Gear
    {
        get
        {
            var parts = new List<string> { $"Weapon: {_c.Weapon?.Name ?? "—"}", $"Armour: {_c.Armor?.Name ?? "—"}" };
            if (_c.Shield is not null) parts.Add($"Shield: {_c.Shield.Name}");
            foreach (var acc in _c.Accessories) parts.Add(acc.Name);
            return string.Join("\n", parts);
        }
    }

    public bool HasMagic => _c.KnownSpells.Count > 0 || _c.KnownSongs.Count > 0;
    public string Magic
    {
        get
        {
            var lines = new List<string>();
            if (_c.KnownSpells.Count > 0)
                lines.Add("Spells: " + string.Join(", ", _c.KnownSpells.Select(id => Spells.Get(id).Name)));
            if (_c.KnownSongs.Count > 0)
                lines.Add("Songs: " + string.Join(", ", _c.KnownSongs.Select(id => Songs.Get(id).Name)));
            return string.Join("\n", lines);
        }
    }

    public IBrush HeaderBrush => _c.IsDead
        ? Brushes.Gray
        : new SolidColorBrush(Color.Parse("#E8C56B"));

    private static string Signed(int n) => n >= 0 ? $"+{n}" : n.ToString();
}

/// <summary>A single attribute chip; the class's prime attribute reads in gold.</summary>
public sealed class AttributeChipViewModel
{
    public AttributeChipViewModel(string text, bool isPrime)
    {
        Text = text;
        IsPrime = isPrime;
    }

    public string Text { get; }
    public bool IsPrime { get; }
    public IBrush Brush => IsPrime
        ? new SolidColorBrush(Color.Parse("#E8C56B"))
        : new SolidColorBrush(Color.Parse("#C7CBDA"));
}
