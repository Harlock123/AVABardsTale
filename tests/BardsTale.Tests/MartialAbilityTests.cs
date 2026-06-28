using System.Collections.Generic;
using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Items;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Covers the martial classes' signature abilities — Cleave, Smite, Backstab, Called Shot and
/// Stunning Strike — their effects, and that each maps to the right class.
/// </summary>
public class MartialAbilityTests
{
    private static (CombatEngine engine, Character hero, Encounter enc) Setup(
        CharacterClass cls, MonsterTemplate foe, int count, int seed = 3)
    {
        var factory = new CharacterFactory(new SystemRandomSource(seed));
        var hero = factory.Create("Hero", Race.Dwarf, cls);
        hero.MaxHitPoints = 9999; hero.HitPoints = 9999;
        hero.Attributes.Strength = 18;          // reliable hits
        hero.Weapon = Items.LongSword;
        var party = new Party();
        party.Add(hero);
        var enc = new Encounter(new[] { new MonsterGroup(foe, count) });
        // Monsters surprised so they don't interfere with the single test round.
        var engine = new CombatEngine(party, enc, new SystemRandomSource(seed), surprise: SurpriseState.MonstersSurprised);
        return (engine, hero, enc);
    }

    private static CombatRound UseAbility(CombatEngine engine, Character hero, MartialAbility ability)
        => engine.ExecuteRound(new[] { new CombatCommand(hero, CombatActionType.Ability, 0, Ability: ability) });

    [Fact]
    public void Each_martial_class_has_a_signature_and_casters_do_not()
    {
        Assert.Equal(MartialAbility.Cleave, MartialAbilities.For(CharacterClass.Warrior));
        Assert.Equal(MartialAbility.Smite, MartialAbilities.For(CharacterClass.Paladin));
        Assert.Equal(MartialAbility.Backstab, MartialAbilities.For(CharacterClass.Rogue));
        Assert.Equal(MartialAbility.CalledShot, MartialAbilities.For(CharacterClass.Hunter));
        Assert.Equal(MartialAbility.StunningStrike, MartialAbilities.For(CharacterClass.Monk));
        Assert.Equal(MartialAbility.None, MartialAbilities.For(CharacterClass.Wizard));
        Assert.Equal(MartialAbility.None, MartialAbilities.For(CharacterClass.Bard));
    }

    [Fact]
    public void Cleave_strikes_multiple_monsters_in_the_group()
    {
        var (engine, hero, enc) = Setup(CharacterClass.Warrior, Bestiary.Ogre, 3);
        var hpBefore = enc.Groups[0].Monsters.Select(m => m.HitPoints).ToList();

        var round = UseAbility(engine, hero, MartialAbility.Cleave);

        // A cleave attempts the whole group (a plain attack hits only one), so several take damage.
        var damaged = enc.Groups[0].Monsters.Where((m, i) => m.HitPoints < hpBefore[i]).Count();
        Assert.True(damaged >= 2, $"cleave should hit several foes at once (damaged {damaged}/3)");
        Assert.Contains(round.Log, l => l.Contains("cleave"));
    }

    [Fact]
    public void Backstab_hits_far_harder_than_a_plain_swing()
    {
        var (engine, hero, enc) = Setup(CharacterClass.Rogue, Bestiary.Troll, 1);
        var before = enc.Groups[0].Monsters[0].HitPoints;

        var round = UseAbility(engine, hero, MartialAbility.Backstab);

        Assert.Contains(round.Log, l => l.Contains("backstabs"));
        var dealt = before - enc.Groups[0].Monsters[0].HitPoints;
        Assert.True(dealt >= hero.EffectiveWeapon.DamageSides * 2, $"backstab should land a heavy blow (dealt {dealt})");
    }

    [Fact]
    public void Stunning_strike_can_stun_a_weak_foe()
    {
        var stunned = 0;
        for (var seed = 0; seed < 20; seed++)
        {
            // A durable Ogre survives the blow, so we can see the stun land rather than just die.
            var (engine, hero, enc) = Setup(CharacterClass.Monk, Bestiary.Ogre, 1, seed);
            UseAbility(engine, hero, MartialAbility.StunningStrike);
            var m = enc.Groups[0].Monsters[0];
            if (!m.IsDead && m.IsAsleep) stunned++;
        }
        Assert.True(stunned > 6, $"a Monk should stun a foe a fair share of the time; stunned {stunned}/20");
    }

    [Fact]
    public void Called_shot_can_fell_a_lesser_foe_outright()
    {
        var felled = 0;
        for (var seed = 0; seed < 30; seed++)
        {
            var (engine, hero, enc) = Setup(CharacterClass.Hunter, Bestiary.HillGiant, 1, seed);
            hero.Weapon = Items.ShortBow; // a ranged shot
            var round = UseAbility(engine, hero, MartialAbility.CalledShot);
            if (round.Log.Any(l => l.Contains("fells"))) felled++;
        }
        Assert.True(felled is > 2 and < 28, $"called shot should sometimes (not always) fell a non-boss; felled {felled}/30");
    }

    [Fact]
    public void Abilities_work_even_in_an_anti_magic_zone()
    {
        var factory = new CharacterFactory(new SystemRandomSource(7));
        var hero = factory.Create("Hero", Race.Dwarf, CharacterClass.Warrior);
        hero.MaxHitPoints = 9999; hero.HitPoints = 9999; hero.Attributes.Strength = 18;
        hero.Weapon = Items.LongSword;
        var party = new Party();
        party.Add(hero);
        var enc = new Encounter(new[] { new MonsterGroup(Bestiary.Ogre, 2) });
        var engine = new CombatEngine(party, enc, new SystemRandomSource(7), magicSuppressed: true, surprise: SurpriseState.MonstersSurprised);
        var before = enc.Groups[0].Monsters.Sum(m => m.HitPoints);

        UseAbility(engine, hero, MartialAbility.Cleave);

        Assert.True(enc.Groups[0].Monsters.Sum(m => m.HitPoints) < before, "a physical manoeuvre should land even where magic is dead");
    }
}
