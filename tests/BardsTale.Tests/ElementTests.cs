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

public class ElementTests
{
    // Casts a power-30 bolt of the given element at one monster and returns the damage dealt.
    // Same seeds + same monster → identical roll, so only the element scaling differs.
    private static int DamageTo(MonsterTemplate t, Element element)
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var caster = party.Members[0];
        var spell = new Spell("TEST", "TEST", "Test Bolt", MagicSchool.Magician, 0, 0,
            SpellEffect.DamageEnemy, SpellTarget.SingleEnemy, 30, "", element);
        var encounter = new Encounter(new[] { new MonsterGroup(t, 1) });
        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 5), surprise: SurpriseState.None);
        var mon = encounter.Groups[0].Monsters[0];
        var before = mon.HitPoints;
        engine.ExecuteRound(new List<CombatCommand> { new(caster, CombatActionType.CastSpell, 0, Spell: spell) });
        return before - mon.HitPoints;
    }

    [Fact]
    public void Hitting_a_weakness_doubles_the_damage()
    {
        // Skeleton is weak to Fire and neutral to Arcane.
        Assert.Equal(2 * DamageTo(Bestiary.Skeleton, Element.Arcane), DamageTo(Bestiary.Skeleton, Element.Fire));
    }

    [Fact]
    public void A_resisted_element_is_halved()
    {
        // Skeleton resists Cold and is neutral to Arcane.
        var neutral = DamageTo(Bestiary.Skeleton, Element.Arcane);
        Assert.Equal(System.Math.Max(1, neutral / 2), DamageTo(Bestiary.Skeleton, Element.Cold));
    }

    [Fact]
    public void An_immune_element_does_nothing()
    {
        // A salamander bathes in flame — fire does not even singe it.
        Assert.Equal(0, DamageTo(Bestiary.Salamander, Element.Fire));
    }

    [Fact]
    public void A_neutral_monster_takes_full_damage_from_any_element()
    {
        // Goblin has no affinities.
        Assert.Equal(DamageTo(Bestiary.Goblin, Element.Arcane), DamageTo(Bestiary.Goblin, Element.Fire));
    }

    [Fact]
    public void The_affinity_table_matches_the_theme()
    {
        Assert.True((MonsterElements.WeakOf("Skeleton") & Element.Fire) != 0);
        Assert.True((MonsterElements.ResistOf("Skeleton") & Element.Poison) != 0);
        Assert.True((MonsterElements.ImmuneOf("Salamander") & Element.Fire) != 0); // bathes in flame
        Assert.True((MonsterElements.ImmuneOf("Elite Salamander") & Element.Fire) != 0); // elites inherit
        Assert.Equal(Element.None, MonsterElements.WeakOf("Goblin")); // neutral
    }

    [Fact]
    public void Scrying_a_foe_reveals_its_affinities_in_the_log()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var caster = party.Members[0];
        caster.SpellPoints = 10;
        var spell = Spells.Get("SCFO"); // Scrye Foe
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Salamander, 1) });
        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 5), surprise: SurpriseState.None);

        var round = engine.ExecuteRound(new List<CombatCommand> { new(caster, CombatActionType.CastSpell, 0, Spell: spell) });

        // The salamander is immune to fire and weak to cold — both should be named, with no damage dealt.
        Assert.Contains(round.Log, l => l.Contains("immune to fire"));
        Assert.Contains(round.Log, l => l.Contains("weak to cold"));
        Assert.Equal(Bestiary.Salamander.MaxHitPoints, encounter.Groups[0].Monsters[0].HitPoints);
    }

    [Fact]
    public void Damage_spells_and_wands_carry_their_element()
    {
        Assert.Equal(Element.Fire, Spells.All.First(s => s.Id == "ARFI").Element);
        Assert.Equal(Element.Cold, Spells.All.First(s => s.Id == "FROS").Element);
        Assert.Equal(Element.Arcane, Spells.All.First(s => s.Id == "MABL").Element); // default
        Assert.Equal(Element.Fire, Items.WandOfFlames.ItemPower!.Element);
        Assert.Equal(Element.Lightning, Items.StaffOfStorms.ItemPower!.Element);
    }
}
