using System.Collections.Generic;
using BardsTale.Core.Characters;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Geometry;
using BardsTale.Core.Persistence;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// The maze-trickery pack: illusory walls (look solid, walk through), one-way doors (seal behind you),
/// and the silent spinner. Covers the wall model, movement, generation connectivity, and save/load.
/// </summary>
public class MazeTrickeryTests
{
    private const int W = 5, H = 5;

    private static (GameState game, Party party) GameWith(System.Action<Maze> setup)
    {
        var rng = new SystemRandomSource(seed: 1);
        var party = NewGame.CreateDefaultParty(rng);
        var maze = new Maze("t", W, H) { StartPosition = new Position(2, 2), StartFacing = Direction.North };
        setup(maze);
        return (new GameState(party, maze, rng), party);
    }

    // --- Illusory walls ---

    [Fact]
    public void An_illusory_wall_reads_as_solid_yet_is_passable()
    {
        var maze = new Maze("t", 3, 3);
        maze.MarkIllusoryWall(1, 1, Direction.North);

        Assert.True(maze[1, 1].HasWall(Direction.North));                       // renders as a wall
        Assert.True(maze.HasIllusoryWall(new Position(1, 1), Direction.North)); // but it's an illusion
        Assert.True(maze.CanMove(new Position(1, 1), Direction.North));         // so the party can pass
    }

    [Fact]
    public void Revealing_an_illusory_wall_leaves_an_open_passage_on_both_sides()
    {
        var maze = new Maze("t", 3, 3);
        maze.MarkIllusoryWall(1, 1, Direction.North);

        maze.RevealIllusoryWall(new Position(1, 1), Direction.North);

        Assert.False(maze[1, 1].HasWall(Direction.North));
        Assert.False(maze[1, 0].HasWall(Direction.South));
        Assert.False(maze.HasIllusoryWall(new Position(1, 1), Direction.North));
    }

    [Fact]
    public void Stepping_into_an_illusory_wall_passes_through_and_dispels_it()
    {
        var (game, party) = GameWith(m => m.MarkIllusoryWall(2, 2, Direction.North));

        var result = game.StepForward(); // from (2,2) facing North

        Assert.Equal(new Position(2, 1), party.Position);          // walked straight through
        Assert.NotNull(result.Note);
        Assert.Contains("illusion", result.Note!);
        Assert.False(game.Maze[2, 2].HasWall(Direction.North));    // the illusion is gone
    }

    // --- One-way doors ---

    [Fact]
    public void A_one_way_door_opens_outward_but_seals_the_return()
    {
        var maze = new Maze("t", 3, 3);
        maze.MarkOneWayDoor(1, 1, Direction.North); // open (1,1)->(1,0), sealed coming back

        Assert.True(maze.CanMove(new Position(1, 1), Direction.North));   // out: open
        Assert.False(maze.CanMove(new Position(1, 0), Direction.South));  // back: blocked
        Assert.False(maze[1, 1].HasWall(Direction.North));               // near side reads open
        Assert.True(maze[1, 0].HasWall(Direction.South));                // far side reads as wall
    }

    [Fact]
    public void A_one_way_door_blocks_the_party_from_returning()
    {
        var (game, party) = GameWith(m => m.MarkOneWayDoor(2, 2, Direction.North));

        var forward = game.StepForward(); // (2,2) -> (2,1) through the one-way
        Assert.Equal(new Position(2, 1), party.Position);
        Assert.NotNull(forward.Note);

        var back = game.StepBackward(); // try to return south to (2,2)
        Assert.Equal(MoveResultKind.BlockedByWall, back.Kind);
        Assert.Equal(new Position(2, 1), party.Position); // didn't budge
    }

    // --- Silent spinner ---

    [Fact]
    public void The_spinner_no_longer_announces_the_new_facing()
    {
        var (game, _) = GameWith(m => m[2, 1].Feature = CellFeature.SpinnerTrap);

        var result = game.StepForward();

        Assert.Equal(MoveResultKind.Spun, result.Kind);
        Assert.DoesNotContain("facing", result.Description); // disorienting — your bearing isn't given away
    }

    // --- Generation never strands the maze (trickery is purely additive) ---

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(42)]
    public void Generated_levels_stay_fully_reachable(int seed)
    {
        var maze = new MazeBuilder(new SystemRandomSource(seed)).Build("t", 12, 12);

        var seen = new HashSet<Position>();
        var stack = new Stack<Position>();
        stack.Push(new Position(0, 0));
        while (stack.Count > 0)
        {
            var p = stack.Pop();
            if (!seen.Add(p)) continue;
            var secrets = maze.SecretDoorsAt(p);
            foreach (Direction d in System.Enum.GetValues<Direction>())
                if (maze.CanMove(p, d)
                    || ((secrets.Contains(d) || maze[p].HasGate(d) || maze[p].HasLockedDoor(d)) && maze.InBounds(p.Step(d))))
                    stack.Push(p.Step(d));
        }

        Assert.Equal(maze.Width * maze.Height, seen.Count);
    }

    // --- Save / load round-trip ---

    [Fact]
    public void Illusory_and_one_way_walls_survive_save_and_load()
    {
        var session = new GameSession(seed: 7);
        session.FillDefaultParty();
        var maze = session.EnterDungeon().Maze;
        maze.MarkIllusoryWall(1, 1, Direction.East);
        maze.MarkOneWayDoor(1, 2, Direction.North);

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session));
        var lmaze = loaded.EnterDungeon().Maze;

        Assert.True(lmaze.HasIllusoryWall(new Position(1, 1), Direction.East));
        Assert.True(lmaze[1, 1].HasWall(Direction.East));               // still reads solid
        Assert.False(lmaze[1, 2].HasWall(Direction.North));            // one-way near side open
        Assert.True(lmaze[1, 1].HasWall(Direction.South));             // one-way far side walled
        Assert.NotEqual(Walls.None, lmaze[1, 2].OneWayDoors & Cell.ToWallFlag(Direction.North));
    }
}
