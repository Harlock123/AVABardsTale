using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Items;
using BardsTale.Core.Util;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Covers front/back rank tactics: ranged weapons let the back rank fight, marching-order
/// reordering changes who stands in the front line, and ranged hits read as shots.
/// </summary>
public class RankTacticsTests
{
    private static Party MakeParty(int count)
    {
        var factory = new CharacterFactory(new SystemRandomSource(seed: 5));
        var party = new Party();
        for (var i = 0; i < count; i++)
            party.Add(factory.Create($"Hero{i}", Race.Human, CharacterClass.Warrior));
        return party;
    }

    [Fact]
    public void A_bow_lets_a_hero_strike_from_the_back_rank()
    {
        var party = MakeParty(1);
        var hero = party.Members[0];

        hero.Weapon = Items.LongSword;
        Assert.False(hero.HasRangedWeapon);

        hero.Weapon = Items.ShortBow;
        Assert.True(hero.HasRangedWeapon);
    }

    [Fact]
    public void Reordering_changes_who_stands_in_the_front_rank()
    {
        var party = MakeParty(5);
        var backliner = party.Members[4];
        Assert.False(party.IsInFrontRank(backliner)); // 5th of five = back rank

        // Bubble them up to the front of the line.
        party.MoveUp(backliner);
        party.MoveUp(backliner);
        Assert.True(party.IsInFrontRank(backliner)); // now in the first three
    }

    [Fact]
    public void Front_rank_is_only_the_first_three_living_heroes()
    {
        var party = MakeParty(5);
        Assert.Equal(3, party.FrontRank.Count());
        Assert.True(party.IsInFrontRank(party.Members[2]));
        Assert.False(party.IsInFrontRank(party.Members[3]));
    }

    [Fact]
    public void A_back_rank_archer_gets_an_attack_option_a_swordsman_does_not()
    {
        var party = MakeParty(4);
        party.Members[3].Weapon = Items.ShortBow;  // back-rank archer
        var melee = MakeParty(4);
        melee.Members[3].Weapon = Items.LongSword; // back-rank swordsman

        Assert.True(BackRankCanAttack(party));
        Assert.False(BackRankCanAttack(melee));
    }

    // Drives the combat VM to the 4th (back-rank) actor and reports whether they were offered an attack.
    private static bool BackRankCanAttack(Party party)
    {
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) });
        var vm = new CombatViewModel(party, encounter, new SystemRandomSource(seed: 2), surprise: SurpriseState.None);
        vm.Begin();

        // Defend with the first three so the prompt advances to the back-rank hero.
        for (var i = 0; i < 3; i++)
            vm.ChooseActionCommand.Execute(vm.Options.First(o => o.Action == CombatActionType.Defend));

        return vm.Options.Any(o => o.Action == CombatActionType.Attack);
    }

    [Fact]
    public void A_ranged_hit_reads_as_a_shot()
    {
        var party = MakeParty(1);
        var archer = party.Members[0];
        archer.Weapon = Items.Crossbow;
        archer.Attributes.Strength = 18; // reliable hit

        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 6) });
        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 1), surprise: SurpriseState.MonstersSurprised);

        var log = engine.ExecuteRound(new[] { new CombatCommand(archer, CombatActionType.Attack, 0) }).Log;

        Assert.Contains(log, l => l.StartsWith($"{archer.Name} shoots") || l.Contains("shot goes wide"));
        Assert.DoesNotContain(log, l => l.StartsWith($"{archer.Name} hits"));
    }
}
