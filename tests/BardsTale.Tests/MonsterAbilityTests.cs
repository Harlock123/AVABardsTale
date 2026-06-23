using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

public class MonsterAbilityTests
{
    [Fact]
    public void Draining_a_level_lowers_level_and_blocks_advancement()
    {
        var c = new CharacterFactory(new SystemRandomSource(seed: 1))
            .Create("Knight", Race.Human, CharacterClass.Warrior);
        c.Level = 5;
        c.MaxHitPoints = 40;
        c.HitPoints = 40;
        c.Experience = 100_000;

        c.DrainLevel();

        Assert.Equal(4, c.Level);
        Assert.True(c.MaxHitPoints < 40);
        Assert.True(c.IsDrained);
        Assert.False(BardsTale.Core.Game.Progression.CanLevelUp(c)); // can't advance while drained, even with the XP
    }

    [Fact]
    public void Restoring_levels_undoes_the_drain_completely()
    {
        var c = new CharacterFactory(new SystemRandomSource(seed: 1))
            .Create("Knight", Race.Human, CharacterClass.Warrior);
        c.Level = 5;
        c.MaxHitPoints = 40;
        c.HitPoints = 40;
        var maxBefore = c.MaxHitPoints;

        c.DrainLevel();
        c.DrainLevel();
        Assert.Equal(3, c.Level);

        c.RestoreLevels();

        Assert.Equal(5, c.Level);
        Assert.Equal(maxBefore, c.MaxHitPoints);
        Assert.False(c.IsDrained);
    }

    [Fact]
    public void Draining_at_level_one_bites_into_max_hp_instead()
    {
        var c = new CharacterFactory(new SystemRandomSource(seed: 1))
            .Create("Squire", Race.Human, CharacterClass.Warrior);
        c.MaxHitPoints = 12;
        c.HitPoints = 12;
        Assert.Equal(1, c.Level);

        c.DrainLevel();

        Assert.Equal(1, c.Level);
        Assert.True(c.MaxHitPoints < 12);
    }

    [Fact]
    public void A_draining_monster_saps_a_level_on_a_hit()
    {
        var rng = new SystemRandomSource(seed: 1);
        var party = new Party();
        var hero = new CharacterFactory(rng).Create("Knight", Race.Human, CharacterClass.Warrior);
        hero.Level = 5;
        hero.MaxHitPoints = 100;
        hero.HitPoints = 100;
        party.Add(hero);

        // AttackBonus 30 guarantees the hit; AbilityChance 1.0 guarantees the drain.
        var drainer = new MonsterTemplate("Wraithling", 20, 5, 1, 4, 30, 0, 0, 1,
            Ability: MonsterAbility.DrainLevel, AbilityChance: 1.0);
        var encounter = new Encounter(new[] { new MonsterGroup(drainer, 1) });
        var engine = new CombatEngine(party, encounter, rng, surprise: SurpriseState.None);

        var round = engine.ExecuteRound(new[] { new CombatCommand(hero, CombatActionType.Defend) });

        Assert.Equal(4, hero.Level);
        Assert.Contains(round.Log, l => l.Contains("drains the life-force"));
    }

    [Fact]
    public void A_thief_monster_steals_party_gold()
    {
        var rng = new SystemRandomSource(seed: 1);
        var party = new Party { Gold = 200 };
        var hero = new CharacterFactory(rng).Create("Guard", Race.Human, CharacterClass.Warrior);
        hero.MaxHitPoints = 100;
        hero.HitPoints = 100;
        party.Add(hero);

        var thief = new MonsterTemplate("Sneak", 8, 8, 1, 4, 30, 0, 0, 1,
            Ability: MonsterAbility.StealGold, AbilityChance: 1.0);
        var encounter = new Encounter(new[] { new MonsterGroup(thief, 1) });
        var engine = new CombatEngine(party, encounter, rng, surprise: SurpriseState.None);

        engine.ExecuteRound(new[] { new CombatCommand(hero, CombatActionType.Defend) });

        Assert.True(party.Gold < 200);
    }

    [Fact]
    public void The_bestiary_wires_up_special_abilities()
    {
        Assert.Equal(MonsterAbility.DrainLevel, Bestiary.Wraith.Ability);
        Assert.Equal(MonsterAbility.StealGold, Bestiary.Cutpurse.Ability);
    }
}
