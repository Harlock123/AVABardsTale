using System;
using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Geometry;
using BardsTale.Core.Persistence;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Covers non-combat dungeon events: the catalogue, generator placement, choice resolution
/// (boons, gambles, costs) and that events are one-shot and survive save/load.
/// </summary>
public class DungeonEventTests
{
    // Catalogue indices, by reading order in DungeonEvents.All.
    private const int Well = 0, Imp = 2, Camp = 4;

    private static (GameState game, Party party, Maze maze) WithEvent(int eventId, int seed = 1)
    {
        var rng = new SystemRandomSource(seed);
        var party = NewGame.CreateDefaultParty(rng);
        var maze = new Maze("t", 1, 1);
        maze.SealBorders();
        maze[new Position(0, 0)].Feature = CellFeature.Event;
        maze[new Position(0, 0)].EventId = eventId;
        var game = new GameState(party, maze, rng);
        party.Position = new Position(0, 0);
        return (game, party, maze);
    }

    [Fact]
    public void Every_event_has_a_prompt_and_options()
    {
        Assert.NotEmpty(DungeonEvents.All);
        foreach (var ev in DungeonEvents.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(ev.Title));
            Assert.False(string.IsNullOrWhiteSpace(ev.Prompt));
            Assert.True(ev.Options.Count >= 2);
            Assert.All(ev.Options, o => Assert.False(string.IsNullOrWhiteSpace(o.Label)));
        }
    }

    [Fact]
    public void Generated_levels_place_an_event_tile()
    {
        var maze = new MazeBuilder(new SystemRandomSource(seed: 7)).Build("t", 11, 11);
        var pos = maze.PositionOf(CellFeature.Event);

        Assert.NotNull(pos);
        Assert.InRange(maze[pos!.Value].EventId, 0, DungeonEvents.Count - 1);
    }

    [Fact]
    public void Stepping_onto_an_event_offers_its_scene()
    {
        var (game, party, _) = WithEvent(Well);
        party.Facing = Direction.North;

        Assert.True(game.OnEvent);
        Assert.NotNull(game.CurrentEvent);
        Assert.Equal("A Wishing Well", game.CurrentEvent!.Title);
    }

    [Fact]
    public void Leaving_an_event_consumes_it()
    {
        var (game, _, maze) = WithEvent(Camp);
        var leave = game.CurrentEvent!.Options.Count - 1; // the last option is always "leave / press on"

        var result = game.ResolveEvent(leave);

        Assert.True(result.Resolved);
        Assert.Equal(CellFeature.None, maze[new Position(0, 0)].Feature); // the scene is gone
    }

    [Fact]
    public void A_wager_swings_the_purse_and_consumes_the_event()
    {
        var (game, party, maze) = WithEvent(Imp);
        party.Gold = 1000;

        var result = game.ResolveEvent(0); // "Wager 50 gold"

        Assert.True(result.Resolved);
        Assert.Contains(party.Gold, new[] { 950, 1050 });            // lost 50 or won 50 net
        Assert.Equal(CellFeature.None, maze[new Position(0, 0)].Feature);
    }

    [Fact]
    public void An_unaffordable_choice_is_not_consumed()
    {
        var (game, party, maze) = WithEvent(Well);
        party.Gold = 0;

        var result = game.ResolveEvent(0); // "make a wish (20 gold)" — can't pay

        Assert.False(result.Resolved);
        Assert.Equal(CellFeature.Event, maze[new Position(0, 0)].Feature); // still there to choose again
    }

    [Fact]
    public void A_restful_event_heals_the_party()
    {
        var (game, party, _) = WithEvent(Camp);
        foreach (var m in party.Members) m.HitPoints = Math.Max(1, m.EffectiveMaxHitPoints / 4);
        var before = party.Members.Sum(m => m.HitPoints);

        game.ResolveEvent(0); // "Rest a while by the embers" (Heal)

        Assert.True(party.Members.Sum(m => m.HitPoints) > before, "resting should restore party HP");
    }

    [Fact]
    public void An_event_tile_survives_save_load()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var session = new GameSession(seed: seed);
            session.FillDefaultParty();
            var maze = session.EnterDungeon().Maze;
            if (maze.PositionOf(CellFeature.Event) is not { } pos) continue;
            var id = maze[pos].EventId;

            var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session)).ActiveDungeon!.Maze;

            Assert.Equal(CellFeature.Event, loaded[pos].Feature);
            Assert.Equal(id, loaded[pos].EventId);
            return;
        }

        Assert.Fail("no seed produced an event tile to round-trip");
    }
}
