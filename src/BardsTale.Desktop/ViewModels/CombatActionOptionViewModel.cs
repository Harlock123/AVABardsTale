using BardsTale.Core.Combat;
using BardsTale.Core.Items;
using BardsTale.Core.Magic;

namespace BardsTale.Desktop.ViewModels;

/// <summary>One choosable action for the character currently awaiting orders.</summary>
public sealed class CombatActionOptionViewModel : ViewModelBase
{
    private CombatActionOptionViewModel(string label, string detail, CombatActionType action,
        Spell? spell, Song? song, Item? item, bool needsEnemyTarget)
    {
        Label = label;
        Detail = detail;
        Action = action;
        Spell = spell;
        Song = song;
        Item = item;
        NeedsEnemyTarget = needsEnemyTarget;
    }

    public string Label { get; }
    public string Detail { get; }
    public CombatActionType Action { get; }
    public Spell? Spell { get; }
    public Song? Song { get; }
    public Item? Item { get; }

    /// <summary>True when resolving this action depends on the selected enemy group.</summary>
    public bool NeedsEnemyTarget { get; }

    /// <summary>True when the player must pick which ally the action affects.</summary>
    public bool NeedsAllyTarget =>
        (Action == CombatActionType.CastSpell && Spell?.Target == SpellTarget.SingleAlly)
        || Action == CombatActionType.UseItem;

    public static CombatActionOptionViewModel Attack() =>
        new("Attack", "strike the selected enemy group", CombatActionType.Attack, null, null, null, true);

    public static CombatActionOptionViewModel Defend() =>
        new("Defend", "brace for blows (harder to hit)", CombatActionType.Defend, null, null, null, false);

    public static CombatActionOptionViewModel Cast(Spell spell) =>
        new($"Cast {spell.Name}", spell.Summary, CombatActionType.CastSpell, spell, null, null, spell.TargetsEnemies);

    public static CombatActionOptionViewModel Sing(Song song) =>
        new($"Sing {song.Name}", song.Summary, CombatActionType.Sing, null, song, null, false);

    public static CombatActionOptionViewModel UseItem(Item item, int count) =>
        new($"Use {item.Name} (x{count})", item.EffectText, CombatActionType.UseItem, null, null, item, false);
}
