using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Persistence;
using BardsTale.Core.Town;
using BardsTale.Core.Util;
using BardsTale.UI.ViewModels;
using Xunit;
using Attr = BardsTale.Core.Characters.Attribute;

namespace BardsTale.Tests;

public class StatDrainTests
{
    private static Character MakeHero(out SystemRandomSource rng)
    {
        rng = new SystemRandomSource(seed: 1);
        var hero = new CharacterFactory(rng).Create("Knight", Race.Human, CharacterClass.Warrior);
        foreach (Attr a in System.Enum.GetValues<Attr>())
            hero.Attributes[a] = 10;
        return hero;
    }

    [Fact]
    public void Draining_a_stat_lowers_it_and_remembers_the_loss()
    {
        var hero = MakeHero(out _);

        hero.DrainAttribute(Attr.Strength);

        Assert.Equal(9, hero.Attributes.Strength);
        Assert.Equal(1, hero.DrainedAttributes.Strength);
        Assert.True(hero.HasDrainedStats);
    }

    [Fact]
    public void A_stat_already_at_the_floor_is_not_falsely_tracked()
    {
        var hero = MakeHero(out _);
        hero.Attributes.Strength = 3;

        hero.DrainAttribute(Attr.Strength);

        Assert.Equal(3, hero.Attributes.Strength);     // floored
        Assert.Equal(0, hero.DrainedAttributes.Strength); // nothing actually lost, so nothing to restore
    }

    [Fact]
    public void Restoring_stats_returns_them_exactly()
    {
        var hero = MakeHero(out _);
        hero.DrainAttribute(Attr.Strength);
        hero.DrainAttribute(Attr.Strength);
        hero.DrainAttribute(Attr.Luck);
        Assert.Equal(8, hero.Attributes.Strength);

        hero.RestoreStats();

        Assert.Equal(10, hero.Attributes.Strength);
        Assert.Equal(10, hero.Attributes.Luck);
        Assert.False(hero.HasDrainedStats);
    }

    [Fact]
    public void A_withering_monster_saps_a_stat_on_a_hit()
    {
        var rng = new SystemRandomSource(seed: 1);
        var party = new Party();
        var hero = MakeHero(out _);
        hero.MaxHitPoints = 100;
        hero.HitPoints = 100;
        party.Add(hero);

        var witherer = new MonsterTemplate("Sapper", 20, 8, 1, 4, 30, 0, 0, 1,
            Ability: MonsterAbility.DrainStat, AbilityChance: 1.0);
        var engine = new CombatEngine(party, new Encounter(new[] { new MonsterGroup(witherer, 1) }), rng, surprise: SurpriseState.None);

        var round = engine.ExecuteRound(new[] { new CombatCommand(hero, CombatActionType.Defend) });

        Assert.True(hero.HasDrainedStats);
        Assert.Contains(round.Log, l => l.Contains("withers"));
    }

    [Fact]
    public void The_temple_restores_withered_stats_for_a_fee()
    {
        var session = new GameSession(seed: 2);
        session.FillDefaultParty();
        session.Party.Gold = 1000;
        var hero = session.Party.Members[0];
        hero.Attributes.Strength = 10;
        hero.DrainAttribute(Attr.Strength);

        session.TownPosition = session.Town.Buildings.First(b => b.Building == TownBuilding.Temple).Position;
        var town = new TownViewModel(session);
        town.EnterCommand.Execute(null);

        var heroVm = town.Party.First(h => h.Model == hero);
        var goldBefore = town.Gold;
        town.RestoreStatsCommand.Execute(heroVm);

        Assert.Equal(10, hero.Attributes.Strength);
        Assert.False(hero.HasDrainedStats);
        Assert.True(town.Gold < goldBefore);
    }

    [Fact]
    public void Drained_stats_survive_a_save_round_trip()
    {
        var session = new GameSession(seed: 2);
        session.FillDefaultParty();
        var hero = session.Party.Members[0];
        hero.Attributes.Dexterity = 12;
        hero.DrainAttribute(Attr.Dexterity);

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session));
        var restored = loaded.Party.Members[0];

        Assert.Equal(hero.DrainedAttributes.Dexterity, restored.DrainedAttributes.Dexterity);
        Assert.Equal(hero.Attributes.Dexterity, restored.Attributes.Dexterity);
        Assert.True(restored.HasDrainedStats);
    }

    [Fact]
    public void The_bestiary_has_a_stat_draining_monster()
        => Assert.Equal(MonsterAbility.DrainStat, Bestiary.CryptCrawler.Ability);
}
