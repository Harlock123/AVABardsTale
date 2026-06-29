using System;
using System.Collections.Generic;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Magic;
using BardsTale.Core.Util;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Monster affixes — encounter-rolled modifiers (Regenerating, Swift, Vampiric, Warded, Savage)
/// that ride on a wandering pack the way the elite prefix does.
/// </summary>
public class MonsterAffixTests
{
    // Power-30 Arcane (neutral) bolt at one monster; same seeds → identical roll, so only the affix differs.
    private static int SpellDamage(MonsterTemplate t)
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var caster = party.Members[0];
        var spell = new Spell("T", "T", "Test", MagicSchool.Magician, 0, 0,
            SpellEffect.DamageEnemy, SpellTarget.SingleEnemy, 30, "", Element.Arcane);
        var encounter = new Encounter(new[] { new MonsterGroup(t, 1) });
        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 5), surprise: SurpriseState.None);
        var mon = encounter.Groups[0].Monsters[0];
        var before = mon.HitPoints;
        engine.ExecuteRound(new List<CombatCommand> { new(caster, CombatActionType.CastSpell, 0, Spell: spell) });
        return before - mon.HitPoints;
    }

    [Fact]
    public void A_warded_monster_takes_reduced_spell_damage()
    {
        var normal = SpellDamage(Bestiary.Goblin);
        var warded = SpellDamage(Bestiary.Goblin with { Affix = MonsterAffix.Warded });
        Assert.Equal(Math.Max(1, (int)Math.Round(normal * 0.6)), warded);
        Assert.True(warded < normal);
    }

    [Fact]
    public void A_vampiric_monster_heals_when_it_wounds_a_hero()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        hero.MaxHitPoints = 100;
        hero.HitPoints = 100;
        var brute = new MonsterTemplate("Brute", 100, 5, 1, 8, 30, 0, 0, 1, Affix: MonsterAffix.Vampiric);
        var encounter = new Encounter(new[] { new MonsterGroup(brute, 1) });
        var monster = encounter.Groups[0].Monsters[0];
        monster.HitPoints = 50; // wounded, so the lifesteal is visible
        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 2), surprise: SurpriseState.PartySurprised);

        engine.ExecuteRound(new List<CombatCommand> { new(hero, CombatActionType.Defend, 0) });

        Assert.True(monster.HitPoints > 50, "a vampiric monster should heal from the blow it lands");
    }

    [Fact]
    public void A_regenerating_monster_knits_its_wounds_each_round()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hulk = new MonsterTemplate("Hulk", 100, 9, 1, 4, 0, 0, 0, 1, Affix: MonsterAffix.Regenerating);
        var encounter = new Encounter(new[] { new MonsterGroup(hulk, 1) });
        var monster = encounter.Groups[0].Monsters[0];
        monster.HitPoints = 50; // wounded
        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 3), surprise: SurpriseState.None);

        engine.ExecuteRound(new List<CombatCommand> { new(party.Members[0], CombatActionType.Defend, 0) });

        Assert.True(monster.HitPoints > 50, "a regenerating monster should heal each round");
    }

    [Fact]
    public void Affixes_can_roll_onto_a_pack_and_raise_its_reward()
    {
        var rng = new SystemRandomSource(seed: 1);
        var affixed = Bestiary.Goblin;
        for (var i = 0; i < 200 && affixed.Affix == MonsterAffix.None; i++)
            affixed = Affixes.MaybeApply(Bestiary.Goblin, depth: 20, rng); // ~30% per try at depth 20

        Assert.NotEqual(MonsterAffix.None, affixed.Affix);
        Assert.True(affixed.ExperienceValue > Bestiary.Goblin.ExperienceValue);
        Assert.True(affixed.GoldValue > Bestiary.Goblin.GoldValue);
    }

    [Fact]
    public void Already_affixed_templates_are_left_alone()
    {
        var warded = Bestiary.Goblin with { Affix = MonsterAffix.Warded };
        Assert.Equal(warded, Affixes.MaybeApply(warded, depth: 20, new SystemRandomSource(seed: 9)));
    }

    private static int FlagCount(MonsterAffix a)
    {
        var n = 0;
        foreach (var f in new[] { MonsterAffix.Regenerating, MonsterAffix.Swift, MonsterAffix.Vampiric, MonsterAffix.Warded, MonsterAffix.Savage })
            if (a.HasFlag(f)) n++;
        return n;
    }

    [Fact]
    public void Deep_floors_can_roll_two_affixes_at_once()
    {
        var rng = new SystemRandomSource(seed: 1);
        var sawDouble = false;
        for (var i = 0; i < 1000 && !sawDouble; i++)
            sawDouble = FlagCount(Affixes.MaybeApply(Bestiary.Goblin, depth: 18, rng).Affix) >= 2;
        Assert.True(sawDouble, "a deep-floor pack should sometimes carry two affixes");
    }

    [Fact]
    public void Shallow_floors_never_roll_two_affixes()
    {
        var rng = new SystemRandomSource(seed: 1);
        for (var i = 0; i < 500; i++)
            Assert.True(FlagCount(Affixes.MaybeApply(Bestiary.Goblin, depth: 5, rng).Affix) <= 1);
    }

    [Fact]
    public void The_roster_card_names_the_affix()
    {
        var group = new MonsterGroup(Bestiary.Skeleton with { Affix = MonsterAffix.Vampiric }, 2);
        var vm = new MonsterGroupViewModel(group, 0);
        Assert.Equal(MonsterAffix.Vampiric, vm.Affix);
        Assert.Contains("Vampiric", vm.Label);
    }

    [Fact]
    public void Combat_announces_an_affixed_pack_with_a_hint()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Skeleton with { Affix = MonsterAffix.Vampiric }, 2) });
        var vm = new CombatViewModel(party, encounter, new SystemRandomSource(seed: 1), surprise: SurpriseState.None);

        vm.Begin();

        Assert.Contains(vm.Log, l => l.Text.Contains("Vampiric") && l.Text.Contains("heals"));
    }
}
