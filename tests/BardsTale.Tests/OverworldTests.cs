using System.Collections.Generic;
using System.Linq;
using BardsTale.Core.Game;
using BardsTale.Core.Geometry;
using BardsTale.Core.Town;
using BardsTale.Core.Util;
using BardsTale.Desktop.ViewModels;
using Xunit;

namespace BardsTale.Tests;

public class TownMapTests
{
    [Fact]
    public void Town_places_all_the_key_buildings()
    {
        var town = TownMapBuilder.Build();
        var kinds = town.Buildings.Select(b => b.Building).ToHashSet();

        Assert.Contains(TownBuilding.Guild, kinds);
        Assert.Contains(TownBuilding.Shop, kinds);
        Assert.Contains(TownBuilding.Temple, kinds);
        Assert.Contains(TownBuilding.ReviewBoard, kinds);
        Assert.Contains(TownBuilding.Tavern, kinds);
        Assert.Contains(TownBuilding.DungeonEntrance, kinds);
        Assert.True(town.Buildings.Count(b => b.Building == TownBuilding.Tavern) >= 2);
    }

    [Fact]
    public void Every_building_is_reachable_from_the_start()
    {
        var town = TownMapBuilder.Build();
        var reachable = Flood(town);
        foreach (var b in town.Buildings)
            Assert.Contains(b.Position, reachable);
    }

    private static HashSet<Position> Flood(TownMap town)
    {
        var seen = new HashSet<Position>();
        var stack = new Stack<Position>();
        stack.Push(town.StartPosition);
        while (stack.Count > 0)
        {
            var p = stack.Pop();
            if (!seen.Add(p)) continue;
            foreach (Direction d in System.Enum.GetValues<Direction>())
                if (town.Streets.CanMove(p, d))
                    stack.Push(p.Step(d));
        }
        return seen;
    }
}

public class TavernTests
{
    [Fact]
    public void A_rumor_comes_from_the_named_tavern_pool()
    {
        var rng = new SystemRandomSource(seed: 1);
        var rumor = Taverns.RandomRumor("The Scarlet Bard", rng);
        Assert.False(string.IsNullOrWhiteSpace(rumor));
    }

    [Fact]
    public void Buying_a_round_costs_gold_and_yields_a_rumor()
    {
        var session = new GameSession(seed: 1);
        session.FillDefaultParty(); // pools starting gold
        session.TownPosition = TavernPosition(session);

        var town = new TownViewModel(session);
        town.EnterCommand.Execute(null);
        Assert.True(town.IsTavern);

        var goldBefore = town.Gold;
        town.BuyRoundCommand.Execute(null);

        Assert.Equal(goldBefore - Taverns.RoundCost, town.Gold);
        Assert.NotEmpty(town.TavernRumors);
    }

    private static Position TavernPosition(GameSession session) =>
        session.Town.Buildings.First(b => b.Building == TownBuilding.Tavern).Position;
}

public class OverworldNavigationTests
{
    [Fact]
    public void Moving_forward_changes_the_party_position()
    {
        var session = new GameSession(seed: 1);
        var town = new TownViewModel(session);

        var startY = town.PartyY;
        town.MoveForwardCommand.Execute(null); // starts facing North (decreasing Y)
        Assert.True(town.PartyY < startY);
    }

    [Fact]
    public void Entering_a_building_opens_its_panel_and_leaving_closes_it()
    {
        var session = new GameSession(seed: 1);
        session.TownPosition = session.Town.Buildings.First(b => b.Building == TownBuilding.Guild).Position;

        var town = new TownViewModel(session);
        Assert.True(town.CanEnter);

        town.EnterCommand.Execute(null);
        Assert.True(town.IsGuild);
        Assert.True(town.IsInBuilding);

        town.LeaveCommand.Execute(null);
        Assert.False(town.IsInBuilding);
    }

    [Fact]
    public void Entering_the_catacomb_stair_requests_the_dungeon()
    {
        var session = new GameSession(seed: 1);
        session.FillDefaultParty();
        session.TownPosition = session.Town.Buildings.First(b => b.Building == TownBuilding.DungeonEntrance).Position;

        var town = new TownViewModel(session);
        var requested = false;
        town.EnterDungeonRequested += () => requested = true;

        town.EnterCommand.Execute(null);

        Assert.True(requested);
        Assert.False(town.IsInBuilding); // the stair doesn't open a panel
    }
}
