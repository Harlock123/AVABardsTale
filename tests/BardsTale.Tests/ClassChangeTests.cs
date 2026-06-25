using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Game;
using BardsTale.Core.Items;
using BardsTale.Core.Persistence;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

public class ClassChangeTests
{
    private static Character Hero(CharacterClass cls, int level)
    {
        var hero = new CharacterFactory(new SystemRandomSource(seed: 5)).Create("Test", Race.Human, cls);
        hero.Level = level;
        return hero;
    }

    [Fact]
    public void Retraining_requires_a_minimum_level_and_a_different_class()
    {
        var hero = Hero(CharacterClass.Warrior, 1);
        Assert.False(Progression.CanChangeClass(hero, CharacterClass.Rogue, out _)); // too low a level

        hero.Level = Progression.MinChangeClassLevel;
        Assert.False(Progression.CanChangeClass(hero, CharacterClass.Warrior, out _)); // already a Warrior
        Assert.True(Progression.CanChangeClass(hero, CharacterClass.Rogue, out _));
    }

    [Fact]
    public void A_drained_hero_cannot_retrain()
    {
        var hero = Hero(CharacterClass.Warrior, 5);
        hero.DrainedLevels = 1;
        Assert.False(Progression.CanChangeClass(hero, CharacterClass.Rogue, out _));
    }

    [Fact]
    public void Retraining_restarts_at_level_one_keeps_hp_and_learns_the_new_craft()
    {
        var hero = Hero(CharacterClass.Warrior, 5);
        hero.Weapon = Items.LongSword; // heavy weapon
        hero.Armor = Items.PlateMail;  // heavy armour
        var hpBefore = hero.MaxHitPoints;

        var displaced = Progression.ChangeClass(hero, CharacterClass.Conjurer);

        Assert.Equal(CharacterClass.Conjurer, hero.Class);
        Assert.Equal(1, hero.Level);
        Assert.Equal(0, hero.Experience);
        Assert.Equal(hpBefore, hero.MaxHitPoints);      // hit points carry over (the reward)
        Assert.True(hero.MaxSpellPoints > 0);           // gains a caster's spell pool
        Assert.NotEmpty(hero.KnownSpells);              // learns the Conjurer's starting spells

        // gear the Conjurer can't use is unequipped and handed back
        Assert.Contains(Items.LongSword, displaced);
        Assert.Contains(Items.PlateMail, displaced);
        Assert.Null(hero.Weapon);
        Assert.Null(hero.Armor);
    }

    [Fact]
    public void Hit_points_accumulate_across_classes()
    {
        var rng = new SystemRandomSource(seed: 9);
        var hero = new CharacterFactory(rng).Create("Test", Race.Human, CharacterClass.Warrior);
        for (var i = 0; i < 4; i++) { hero.Experience = hero.ExperienceForNextLevel; Progression.TryLevelUp(hero, rng); }
        var hpAsWarrior = hero.MaxHitPoints;

        Progression.ChangeClass(hero, CharacterClass.Conjurer);
        Assert.Equal(hpAsWarrior, hero.MaxHitPoints);

        hero.Experience = hero.ExperienceForNextLevel;
        Progression.TryLevelUp(hero, rng);
        Assert.True(hero.MaxHitPoints >= hpAsWarrior, "re-levelling only adds more HP");
    }

    [Fact]
    public void A_retrained_hero_round_trips_through_a_save()
    {
        var session = new GameSession(seed: 4);
        session.FillDefaultParty();
        var hero = session.Party.Members[0];
        hero.Level = 5;
        Progression.ChangeClass(hero, CharacterClass.Wizard);
        var spellsKnown = hero.KnownSpells.Count;

        var reloaded = GameSerializer.FromJson(GameSerializer.ToJson(session)).Party.Members[0];

        Assert.Equal(CharacterClass.Wizard, reloaded.Class);
        Assert.Equal(1, reloaded.Level);
        Assert.Equal(spellsKnown, reloaded.KnownSpells.Count);
    }
}
