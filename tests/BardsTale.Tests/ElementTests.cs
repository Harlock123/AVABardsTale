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
        // Salamander resists Fire and is neutral to Arcane.
        var neutral = DamageTo(Bestiary.Salamander, Element.Arcane);
        Assert.Equal(System.Math.Max(1, neutral / 2), DamageTo(Bestiary.Salamander, Element.Fire));
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
        Assert.True((MonsterElements.ResistOf("Salamander") & Element.Fire) != 0);
        Assert.Equal(Element.None, MonsterElements.WeakOf("Goblin")); // neutral
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
