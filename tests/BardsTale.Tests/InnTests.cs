using System.Linq;
using BardsTale.Core.Game;
using BardsTale.Core.Town;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

public class InnTests
{
    private static TownViewModel InnWith(out GameSession session, int gold = 1000)
    {
        session = new GameSession(seed: 2);
        session.FillDefaultParty();
        session.Party.Gold = gold;
        session.TownPosition = session.Town.Buildings.First(b => b.Building == TownBuilding.Inn).Position;
        var town = new TownViewModel(session);
        town.EnterCommand.Execute(null);
        return town;
    }

    [Fact]
    public void The_town_has_an_inn()
        => Assert.Contains(TownMapBuilder.Build().Buildings, b => b.Building == TownBuilding.Inn);

    [Fact]
    public void Resting_restores_hp_and_sp_for_a_fee()
    {
        var town = InnWith(out var session);
        Assert.True(town.IsInn);

        var caster = session.Party.Members.First(m => m.IsSpellcaster);
        caster.HitPoints = 1;
        caster.SpellPoints = 0;
        var goldBefore = town.Gold;

        town.RestCommand.Execute(null);

        Assert.Equal(caster.MaxHitPoints, caster.HitPoints);
        Assert.Equal(caster.MaxSpellPoints, caster.SpellPoints);
        Assert.True(town.Gold < goldBefore);
    }

    [Fact]
    public void Resting_does_not_raise_the_dead()
    {
        var town = InnWith(out var session);
        var fallen = session.Party.Members[1];
        fallen.ApplyDamage(fallen.MaxHitPoints + 10);
        Assert.True(fallen.IsDead);

        town.RestCommand.Execute(null);

        Assert.True(fallen.IsDead);
    }

    [Fact]
    public void Resting_is_refused_without_enough_gold()
    {
        var town = InnWith(out var session, gold: 0);
        var caster = session.Party.Members.First(m => m.IsSpellcaster);
        caster.HitPoints = 1;

        town.RestCommand.Execute(null);

        Assert.Equal(1, caster.HitPoints); // no rest happened
        Assert.Equal(0, town.Gold);
    }
}
