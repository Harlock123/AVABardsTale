using Avalonia.Media;
using BardsTale.Core.Combat;
using BardsTale.Core.Items;
using BardsTale.Core.Magic;

namespace BardsTale.UI.ViewModels;

/// <summary>One choosable action for the character currently awaiting orders.</summary>
public sealed class CombatActionOptionViewModel : ViewModelBase
{
    private CombatActionOptionViewModel(string label, string detail, CombatActionType action,
        Spell? spell, Song? song, Item? item, bool needsEnemyTarget, bool isItemPower = false,
        int spellCost = 0, bool canAfford = true)
    {
        Label = label;
        Detail = detail;
        Action = action;
        Spell = spell;
        Song = song;
        Item = item;
        NeedsEnemyTarget = needsEnemyTarget;
        IsItemPower = isItemPower;
        SpellCost = spellCost;
        CanAfford = canAfford;
    }

    /// <summary>True when this action is a once-per-fight power from a wielded item.</summary>
    public bool IsItemPower { get; }

    /// <summary>The spell-point cost of this action (only meaningful for cast-spell options).</summary>
    public int SpellCost { get; }

    /// <summary>True unless this is a spell the caster can't currently pay for.</summary>
    public bool CanAfford { get; }

    /// <summary>True for a real spell with an SP cost (not a free item power or a non-spell action).</summary>
    public bool ShowsCost => Action == CombatActionType.CastSpell && !IsItemPower;

    public string CostText => ShowsCost ? $"{SpellCost} SP" : "";

    /// <summary>Affordable actions read normally; spells the caster can't pay for dim out.</summary>
    public IBrush LabelBrush => CanAfford
        ? new SolidColorBrush(Color.Parse("#E8E9F0"))
        : new SolidColorBrush(Color.Parse("#6B7280"));

    public IBrush CostBrush => CanAfford
        ? new SolidColorBrush(Color.Parse("#7FB069"))
        : new SolidColorBrush(Color.Parse("#C0566B"));

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

    /// <summary>A back-rank attack with a ranged weapon — resolves like Attack, reaching past the front line.</summary>
    public static CombatActionOptionViewModel Shoot() =>
        new("Shoot", "loose a ranged shot at the selected enemy group", CombatActionType.Attack, null, null, null, true);

    public static CombatActionOptionViewModel Defend() =>
        new("Defend", "brace for blows (harder to hit)", CombatActionType.Defend, null, null, null, false);

    public static CombatActionOptionViewModel Cast(Spell spell, int casterSp) =>
        new($"Cast {spell.Name}", spell.Summary, CombatActionType.CastSpell, spell, null, null, spell.TargetsEnemies,
            spellCost: spell.Cost, canAfford: casterSp >= spell.Cost);

    public static CombatActionOptionViewModel Sing(Song song) =>
        new($"Sing {song.Name}", song.Summary, CombatActionType.Sing, null, song, null, false);

    public static CombatActionOptionViewModel UseItem(Item item, int count) =>
        new($"Use {item.Name} (x{count})", item.EffectText, CombatActionType.UseItem, null, null, item, false);

    /// <summary>Fire a wielded item's once-per-fight power — resolves like a free spell.</summary>
    public static CombatActionOptionViewModel UsePower(Item item)
    {
        var p = item.ItemPower!;
        var detail = p.Effect switch
        {
            SpellEffect.DamageEnemy or SpellEffect.DamageAllEnemies => $"once per fight · ~{p.Power} dmg",
            SpellEffect.DrainEnemy => $"once per fight · ~{p.Power} dmg, heal self",
            SpellEffect.Revive => "once per fight · revive an ally",
            SpellEffect.HealAlly or SpellEffect.HealParty => $"once per fight · ~{p.Power} heal",
            SpellEffect.HasteParty => $"once per fight · party +{p.Power} attack/round",
            SpellEffect.RegenParty => $"once per fight · party regen {p.Power}/round",
            SpellEffect.BuffPartyArmor => $"once per fight · party AC +{p.Power}",
            SpellEffect.BuffPartyAttack => $"once per fight · party hits +{p.Power}",
            SpellEffect.CleanseParty => "once per fight · cure the party",
            SpellEffect.RestorePartySpellPoints => $"once per fight · party +{p.Power} SP",
            _ => "once per fight"
        };
        return new($"⚡ Use {item.Name}", detail, CombatActionType.CastSpell, p, null, item, p.TargetsEnemies,
            isItemPower: true);
    }
}
