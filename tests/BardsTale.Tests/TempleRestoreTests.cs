using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Game;
using BardsTale.Core.Persistence;
using BardsTale.Core.Town;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

public class TempleRestoreTests
{
    [Fact]
    public void The_temple_restores_drained_levels_for_a_fee()
    {
        var session = new GameSession(seed: 2);
        session.FillDefaultParty();
        session.Party.Gold = 1000;

        var hero = session.Party.Members[0];
        hero.Level = 5;
        hero.MaxHitPoints = 40;
        hero.HitPoints = 40;
        hero.DrainLevel();
        Assert.Equal(4, hero.Level);

        session.TownPosition = session.Town.Buildings.First(b => b.Building == TownBuilding.Temple).Position;
        var town = new TownViewModel(session);
        town.EnterCommand.Execute(null);
        Assert.True(town.IsTemple);

        var heroVm = town.Party.First(h => h.Model == hero);
        var goldBefore = town.Gold;
        town.RestoreLevelsCommand.Execute(heroVm);

        Assert.Equal(5, hero.Level);
        Assert.False(hero.IsDrained);
        Assert.True(town.Gold < goldBefore);
    }

    [Fact]
    public void Drain_state_survives_a_save_round_trip()
    {
        var session = new GameSession(seed: 2);
        session.FillDefaultParty();
        var hero = session.Party.Members[0];
        hero.Level = 5;
        hero.DrainLevel();
        hero.DrainLevel();

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session));
        var restored = loaded.Party.Members[0];

        Assert.Equal(hero.Level, restored.Level);
        Assert.Equal(hero.DrainedLevels, restored.DrainedLevels);
        Assert.Equal(hero.DrainedHitPoints, restored.DrainedHitPoints);
        Assert.True(restored.IsDrained);
    }
}
