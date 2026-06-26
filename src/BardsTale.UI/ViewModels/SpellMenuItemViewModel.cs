using Avalonia.Media;
using BardsTale.Core.Characters;
using BardsTale.Core.Magic;

namespace BardsTale.UI.ViewModels;

/// <summary>One castable utility spell in the town spell menu, paired with who can cast it.</summary>
public sealed class SpellMenuItemViewModel : ViewModelBase
{
    public SpellMenuItemViewModel(Character caster, Spell spell)
    {
        Caster = caster;
        Spell = spell;
    }

    public Character Caster { get; }
    public Spell Spell { get; }

    public bool NeedsTarget => Spell.Target == SpellTarget.SingleAlly;
    public string Label => $"{Caster.Name}: {Spell.Name}";
    public string Detail => Spell.Summary;

    /// <summary>True when the caster currently has the spell points to cast it.</summary>
    public bool CanAfford => Caster.SpellPoints >= Spell.Cost;

    public string CostText => $"{Spell.Cost} SP";
    public string CasterSpText => $"{Caster.Name} has {Caster.SpellPoints}/{Caster.EffectiveMaxSpellPoints} SP";

    /// <summary>Affordable spells read normally; ones the caster can't yet pay for dim out.</summary>
    public IBrush LabelBrush => CanAfford
        ? new SolidColorBrush(Color.Parse("#E8E9F0"))
        : new SolidColorBrush(Color.Parse("#6B7280"));

    public IBrush CostBrush => CanAfford
        ? new SolidColorBrush(Color.Parse("#7FB069"))
        : new SolidColorBrush(Color.Parse("#C0566B"));
}
