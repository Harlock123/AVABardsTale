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
}
