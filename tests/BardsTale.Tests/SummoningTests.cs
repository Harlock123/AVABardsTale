using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Util;
using BardsTale.Desktop.ViewModels;
using Xunit;

namespace BardsTale.Tests;

public class SummoningTests
{
    private static MonsterTemplate Summoner(int hp = 200) => new(
        "Caller", hp, 8, 1, 4, 0, 0, 0, 1, Speed: 1,
        Spell: new MonsterSpell("Summon", MonsterSpellKind.Summon, 2, 1.0, Bestiary.Skeleton));

    [Fact]
    public void Reinforcements_add_a_group_until_the_cap()
    {
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) });

        var added = encounter.AddReinforcements(Bestiary.Skeleton, 3);
        Assert.NotNull(added);
        Assert.Equal(2, encounter.Groups.Count);
        Assert.Equal(3, added!.Monsters.Count);

        while (encounter.CanSummonMore)
            encounter.AddReinforcements(Bestiary.Skeleton, 1);

        Assert.Equal(Encounter.MaxGroups, encounter.Groups.Count);
        Assert.Null(encounter.AddReinforcements(Bestiary.Skeleton, 1)); // capped
    }

    [Fact]
    public void A_summoner_calls_reinforcements_into_the_fight()
    {
        var rng = new SystemRandomSource(seed: 1);
        var party = new Party();
        var hero = new CharacterFactory(rng).Create("Guard", Race.Human, CharacterClass.Warrior);
        hero.MaxHitPoints = 100;
        hero.HitPoints = 100;
        party.Add(hero);

        var encounter = new Encounter(new[] { new MonsterGroup(Summoner(), 1) });
        var before = encounter.Groups.Count;
        var engine = new CombatEngine(party, encounter, rng, surprise: SurpriseState.None);

        var round = engine.ExecuteRound(new[] { new CombatCommand(hero, CombatActionType.Defend) });

        Assert.True(encounter.Groups.Count > before);
        Assert.Contains(encounter.Groups, g => g.Template == Bestiary.Skeleton);
        Assert.Contains(round.Log, l => l.Contains("answer the call"));
    }

    [Fact]
    public void The_bestiary_has_a_summoning_monster()
    {
        Assert.Equal(MonsterSpellKind.Summon, Bestiary.Necromancer.Spell!.Kind);
        Assert.Equal(Bestiary.Skeleton, Bestiary.Necromancer.Spell!.SummonTemplate);
    }

    [Fact]
    public void Summoned_groups_appear_in_the_combat_view()
    {
        var rng = new SystemRandomSource(seed: 1);
        var party = new Party();
        var hero = new CharacterFactory(rng).Create("Guard", Race.Human, CharacterClass.Warrior);
        hero.MaxHitPoints = 100;
        hero.HitPoints = 100;
        party.Add(hero);

        var encounter = new Encounter(new[] { new MonsterGroup(Summoner(), 1) });
        var vm = new CombatViewModel(party, encounter, rng, surprise: SurpriseState.None);
        vm.Begin();
        var groupsBefore = vm.Groups.Count;

        vm.AutoCommand.Execute(null); // resolves a round; the tanky summoner survives and summons

        Assert.True(vm.Groups.Count > groupsBefore);
    }
}
