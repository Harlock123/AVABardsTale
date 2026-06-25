using System.Collections.Generic;
using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Items;
using BardsTale.Core.Magic;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

public class DrainReviveSpellTests
{
    private static Party TankyParty()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        foreach (var m in party.Members) { m.MaxHitPoints = 9999; m.HitPoints = 9999; }
        return party;
    }

    [Fact]
    public void The_drain_wand_damages_a_foe_and_heals_the_wielder()
    {
        var party = TankyParty();
        var wielder = party.Members[0];
        wielder.Weapon = Items.WandOfLeeching;
        wielder.HitPoints = 100;

        var rng = new SystemRandomSource(seed: 3);
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) });
        var engine = new CombatEngine(party, encounter, rng, surprise: SurpriseState.None);

        var round = engine.ExecuteRound(new List<CombatCommand>
        {
            new(wielder, CombatActionType.CastSpell, 0, Spell: Items.WandOfLeeching.ItemPower)
        });

        Assert.Contains(round.Log, l => l.Contains("draining"));
        Assert.True(wielder.HitPoints > 100, "the wand should heal its wielder");
    }

    [Fact]
    public void The_resurrection_rod_raises_a_fallen_ally()
    {
        var party = TankyParty();
        var caster = party.Members[0];
        caster.Weapon = Items.RodOfResurrection;
        var fallen = party.Members[1];
        fallen.ApplyDamage(99999);
        Assert.True(fallen.IsDead);

        var rng = new SystemRandomSource(seed: 3);
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) });
        var engine = new CombatEngine(party, encounter, rng, surprise: SurpriseState.None);

        engine.ExecuteRound(new List<CombatCommand>
        {
            new(caster, CombatActionType.CastSpell, 0, Spell: Items.RodOfResurrection.ItemPower, TargetAllyIndex: 1)
        });

        Assert.False(fallen.IsDead);
        Assert.True(fallen.HitPoints > 0);
    }

    [Fact]
    public void Every_item_power_effect_exists_as_a_learnable_spell()
    {
        var effects = Spells.All.Select(s => s.Effect).ToHashSet();
        foreach (var effect in new[]
                 {
                     SpellEffect.DrainEnemy, SpellEffect.HasteParty, SpellEffect.RegenParty,
                     SpellEffect.CleanseParty, SpellEffect.RestorePartySpellPoints
                 })
            Assert.Contains(effect, effects);

        // healer effects sit with the Conjurer; offensive ones with the war-mages
        Assert.Equal(MagicSchool.Conjurer, Spells.All.First(s => s.Effect == SpellEffect.RegenParty).School);
        Assert.Equal(MagicSchool.Conjurer, Spells.All.First(s => s.Effect == SpellEffect.CleanseParty).School);
        Assert.NotEqual(MagicSchool.Conjurer, Spells.All.First(s => s.Effect == SpellEffect.DrainEnemy).School);
        Assert.NotEqual(MagicSchool.Conjurer, Spells.All.First(s => s.Effect == SpellEffect.HasteParty).School);

        // and they're actually granted by levelling
        Assert.Contains(Spells.LearnedAtLevel(MagicSchool.Sorcerer, 2), s => s.Effect == SpellEffect.DrainEnemy);
    }
}
