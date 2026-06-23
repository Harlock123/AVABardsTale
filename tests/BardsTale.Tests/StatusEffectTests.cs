using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Magic;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

public class StatusEffectTests
{
    [Fact]
    public void Inflict_and_cure_toggle_the_status_flags()
    {
        var rng = new SystemRandomSource(seed: 1);
        var hero = new CharacterFactory(rng).Create("Test", Race.Human, CharacterClass.Warrior);

        hero.Inflict(StatusEffect.Poisoned);
        hero.Inflict(StatusEffect.Paralyzed);
        Assert.True(hero.HasAilment);
        Assert.True(hero.IsPoisoned);

        hero.CureAilments();
        Assert.False(hero.HasAilment);
        Assert.False(hero.IsPoisoned);
        Assert.False(hero.IsParalyzed);
    }

    [Fact]
    public void Sleeping_or_paralysed_members_cannot_act()
    {
        var rng = new SystemRandomSource(seed: 1);
        var hero = new CharacterFactory(rng).Create("Test", Race.Human, CharacterClass.Warrior);

        hero.Inflict(StatusEffect.Asleep);
        Assert.False(hero.CanAct);
        hero.Wake();
        Assert.True(hero.CanAct);

        hero.Inflict(StatusEffect.Paralyzed);
        Assert.False(hero.CanAct);
    }

    [Fact]
    public void Dead_characters_cannot_be_poisoned()
    {
        var rng = new SystemRandomSource(seed: 1);
        var hero = new CharacterFactory(rng).Create("Test", Race.Human, CharacterClass.Warrior);
        hero.ApplyDamage(hero.MaxHitPoints + 10);

        hero.Inflict(StatusEffect.Poisoned);
        Assert.False(hero.IsPoisoned);
    }

    [Fact]
    public void Poison_bites_at_the_start_of_a_combat_round()
    {
        var rng = new SystemRandomSource(seed: 5);
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 5));
        var hero = party.Members.First();
        hero.Inflict(StatusEffect.Poisoned);

        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) });
        var engine = new CombatEngine(party, encounter, rng, surprise: SurpriseState.None);

        var before = hero.HitPoints;
        var round = engine.ExecuteRound(new[] { new CombatCommand(hero, CombatActionType.Defend) });

        Assert.Contains(round.Log, l => l.Contains("poison damage"));
        Assert.True(hero.HitPoints < before);
    }

    [Fact]
    public void Purify_spell_cleanses_an_afflicted_ally()
    {
        var rng = new SystemRandomSource(seed: 5);
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 5));
        var hero = party.Members.First();
        hero.Inflict(StatusEffect.Poisoned);

        var conjurer = party.Members.First(m => m.School == MagicSchool.Conjurer);
        conjurer.KnownSpells.Add("PURE");
        conjurer.SpellPoints = 10;

        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) });
        var engine = new CombatEngine(party, encounter, rng, surprise: SurpriseState.None);

        engine.ExecuteRound(new[]
        {
            new CombatCommand(conjurer, CombatActionType.CastSpell,
                Spell: Spells.Get("PURE"), TargetAllyIndex: party.Members.ToList().IndexOf(hero))
        });

        Assert.False(hero.IsPoisoned);
    }

    [Fact]
    public void Resting_at_a_safe_spot_cures_ailments()
    {
        var rng = new SystemRandomSource(seed: 5);
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 5));
        var hero = party.Members.First();
        hero.Inflict(StatusEffect.Poisoned);
        hero.Inflict(StatusEffect.Paralyzed);

        party.Rest();
        Assert.False(hero.HasAilment);
    }

    [Fact]
    public void Status_inflicting_monsters_exist_in_the_bestiary()
    {
        Assert.Equal(StatusEffect.Poisoned, Bestiary.GiantSpider.InflictsStatus);
        Assert.Equal(StatusEffect.Paralyzed, Bestiary.Ghoul.InflictsStatus);
        Assert.Equal(StatusEffect.Asleep, Bestiary.WillOWisp.InflictsStatus);
    }
}
