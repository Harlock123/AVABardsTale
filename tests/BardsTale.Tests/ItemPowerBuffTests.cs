using System.Collections.Generic;
using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Items;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

public class ItemPowerBuffTests
{
    private static Party TankyParty()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        foreach (var m in party.Members) { m.MaxHitPoints = 9999; m.HitPoints = 9999; }
        return party;
    }

    // member 0 invokes the power; everyone else defends out of the way.
    private static CombatRound Invoke(Party party, Item powerItem, MonsterTemplate dummy)
    {
        var rng = new SystemRandomSource(seed: 7);
        var encounter = new Encounter(new[] { new MonsterGroup(dummy, 1) });
        var engine = new CombatEngine(party, encounter, rng, surprise: SurpriseState.None);
        var cmds = new List<CombatCommand>
        {
            new(party.Members[0], CombatActionType.CastSpell, 0, Spell: powerItem.ItemPower)
        };
        cmds.AddRange(party.Members.Skip(1).Select(m => new CombatCommand(m, CombatActionType.Defend)));
        return engine.ExecuteRound(cmds);
    }

    [Fact]
    public void Haste_grants_the_party_one_extra_attack_each_round()
    {
        int Swings(bool haste)
        {
            var party = TankyParty();
            var attacker = party.Members[1];
            attacker.Weapon = Items.LongSword;
            var rng = new SystemRandomSource(seed: 4);
            var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Tarrasque, 1) }); // tanky, harmless dummy
            var engine = new CombatEngine(party, encounter, rng, surprise: SurpriseState.None);

            var cmds = new List<CombatCommand> { new(attacker, CombatActionType.Attack, 0) };
            for (var i = 2; i < party.Members.Count; i++)
                cmds.Add(new CombatCommand(party.Members[i], CombatActionType.Defend));
            if (haste)
                cmds.Add(new CombatCommand(party.Members[0], CombatActionType.CastSpell, 0, Spell: Items.BannerOfHaste.ItemPower));

            var round = engine.ExecuteRound(cmds);
            return round.Log.Count(l => l.StartsWith($"{attacker.Name} hits") || l.StartsWith($"{attacker.Name} misses"));
        }

        Assert.Equal(Swings(haste: false) + 1, Swings(haste: true));
    }

    [Fact]
    public void Aura_of_renewal_mends_the_party_each_round()
    {
        var party = TankyParty();
        var wounded = party.Members[1];
        wounded.HitPoints = 100;

        var round = Invoke(party, Items.StandardOfRenewal, Bestiary.GiantRat);

        Assert.Contains(round.Log, l => l.Contains("restoring aura mends"));
        Assert.True(wounded.HitPoints > 100, "regen should net positive against a feeble foe");
    }

    [Fact]
    public void Cleansing_peal_cures_party_ailments()
    {
        var party = TankyParty();
        var afflicted = party.Members[1];
        afflicted.Inflict(StatusEffect.Poisoned);

        Invoke(party, Items.ChimeOfCleansing, Bestiary.Skeleton); // skeleton can't re-poison

        Assert.False(afflicted.IsPoisoned);
    }

    [Fact]
    public void Mana_font_restores_party_spell_points()
    {
        var party = TankyParty();
        var caster = party.Members[1];
        caster.MaxSpellPoints = 20;
        caster.SpellPoints = 0;

        Invoke(party, Items.OrbOfMana, Bestiary.Skeleton);

        Assert.True(caster.SpellPoints > 0);
    }
}
