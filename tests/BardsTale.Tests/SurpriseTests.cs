using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Util;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

public class SurpriseTests
{
    // AttackBonus 30 guarantees the monster hits whenever it gets to act.
    private static MonsterTemplate Brute(int hp = 100) => new("Brute", hp, 5, 1, 6, 30, 0, 0, 1);

    private static (CombatEngine engine, Character hero, Monster monster) Setup(
        SurpriseState surprise, int monsterHp = 100)
    {
        var rng = new SystemRandomSource(seed: 1);
        var party = new Party();
        var hero = new CharacterFactory(rng).Create("Knight", Race.Human, CharacterClass.Warrior);
        hero.MaxHitPoints = 100;
        hero.HitPoints = 100;
        party.Add(hero);

        var encounter = new Encounter(new[] { new MonsterGroup(Brute(monsterHp), 1) });
        var engine = new CombatEngine(party, encounter, rng, surprise: surprise);
        return (engine, hero, encounter.Groups[0].Monsters[0]);
    }

    [Fact]
    public void A_monster_surprise_lets_the_party_strike_first_unanswered()
    {
        var (engine, hero, monster) = Setup(SurpriseState.MonstersSurprised);
        var monsterHpBefore = monster.HitPoints;

        engine.ExecuteRound(new[] { new CombatCommand(hero, CombatActionType.Attack, 0) });

        Assert.Equal(100, hero.HitPoints);          // the monster never got to swing
        Assert.True(monster.HitPoints < monsterHpBefore); // the hero did
    }

    [Fact]
    public void An_ambush_lets_only_the_monsters_act()
    {
        var (engine, hero, monster) = Setup(SurpriseState.PartySurprised);
        var monsterHpBefore = monster.HitPoints;

        engine.ExecuteRound(new[] { new CombatCommand(hero, CombatActionType.Attack, 0) });

        Assert.Equal(monsterHpBefore, monster.HitPoints); // the hero's attack was ignored
        Assert.True(hero.HitPoints < 100);                // the monster struck
    }

    [Fact]
    public void Surprise_only_applies_to_the_opening_round()
    {
        var (engine, hero, _) = Setup(SurpriseState.MonstersSurprised);

        engine.ExecuteRound(new[] { new CombatCommand(hero, CombatActionType.Attack, 0) });
        Assert.Equal(100, hero.HitPoints); // round 1: monster skipped

        engine.ExecuteRound(new[] { new CombatCommand(hero, CombatActionType.Attack, 0) });
        Assert.True(hero.HitPoints < 100); // round 2: monster acts normally
    }

    [Fact]
    public void The_combat_view_narrates_an_ambush_and_continues()
    {
        var rng = new SystemRandomSource(seed: 1);
        var party = NewGame.CreateDefaultParty(rng); // a full party survives one ambush round
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) });

        var vm = new CombatViewModel(party, encounter, rng, surprise: SurpriseState.PartySurprised);
        vm.Begin();

        Assert.Contains(vm.Log, l => l.Contains("ambushed"));
        Assert.False(vm.IsOver);
        Assert.NotEmpty(vm.Options); // the party's own (second) round of orders has begun
    }
}
