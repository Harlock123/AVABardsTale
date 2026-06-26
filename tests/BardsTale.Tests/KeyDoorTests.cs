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
/// Covers carried keys and locked doors: a locked door blocks until the party spends a key,
/// keys are pocketed from the floor, and the generator always pairs a locked vault with its key.
/// </summary>
public class KeyDoorTests
{
    // A tiny corridor (0,0)-(1,0)-(2,0), open between cells, sealed at the borders.
    private static (GameState game, Party party, Maze maze) Corridor(int seed = 1)
    {
        var rng = new SystemRandomSource(seed);
        var party = NewGame.CreateDefaultParty(rng);
        var maze = new Maze("t", 3, 1);
        maze.SealBorders();
        var game = new GameState(party, maze, rng);
        return (game, party, maze);
    }

    [Fact]
    public void A_locked_door_blocks_a_keyless_party()
    {
        var (game, party, maze) = Corridor();
        maze.MarkLockedDoor(1, 0, Direction.East);
        party.Position = new Position(1, 0);
        party.Facing = Direction.East;
        party.Keys = 0;

        var result = game.StepForward();

        Assert.Equal(MoveResultKind.BlockedByWall, result.Kind);
        Assert.Contains("key", result.Description);
        Assert.Equal(new Position(1, 0), party.Position);
    }

    [Fact]
    public void A_key_unlocks_the_door_and_is_spent()
    {
        var (game, party, maze) = Corridor();
        maze.MarkLockedDoor(1, 0, Direction.East);
        party.Position = new Position(1, 0);
        party.Facing = Direction.East;
        party.Keys = 1;

        var unlock = game.StepForward();

        Assert.Equal(MoveResultKind.Unlocked, unlock.Kind);
        Assert.Equal(0, party.Keys);                                   // the key was consumed
        Assert.True(maze.CanMove(new Position(1, 0), Direction.East)); // the way is open
        Assert.Equal(new Position(1, 0), party.Position);              // unlocking doesn't move you

        game.StepForward();
        Assert.Equal(new Position(2, 0), party.Position);              // now you can step through
    }

    [Fact]
    public void Stepping_onto_a_key_pockets_it()
    {
        var rng = new SystemRandomSource(seed: 1);
        var party = NewGame.CreateDefaultParty(rng);
        var maze = new Maze("t", 2, 1);
        maze.SealBorders();
        maze[new Position(1, 0)].Feature = CellFeature.Key;
        var game = new GameState(party, maze, rng);
        party.Position = new Position(0, 0);
        party.Facing = Direction.East;

        var result = game.StepForward();

        Assert.Equal(MoveResultKind.KeyFound, result.Kind);
        Assert.Equal(1, party.Keys);
        Assert.Equal(CellFeature.None, maze[new Position(1, 0)].Feature); // the key is gone from the floor
    }

    [Fact]
    public void Generated_levels_pair_a_locked_door_with_a_key()
    {
        var withPuzzle = 0;
        for (var seed = 0; seed < 12; seed++)
        {
            var maze = new MazeBuilder(new SystemRandomSource(seed)).Build("t", 11, 11);
            var key = maze.PositionOf(CellFeature.Key);
            var locked = maze.LockedDoorCount();

            if (locked > 0) Assert.NotNull(key);          // a locked vault always has a findable key
            if (key is not null)
            {
                Assert.True(locked > 0, "a dropped key should always have a locked door to open");
                withPuzzle++;
            }
        }

        Assert.True(withPuzzle >= 8, $"only {withPuzzle}/12 generated levels had the key puzzle");
    }

    [Fact]
    public void Carried_keys_and_a_locked_door_survive_save_load()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var session = new GameSession(seed: seed);
            session.FillDefaultParty();
            var maze = session.EnterDungeon().Maze;

            if (maze.LockedDoorCount() == 0) continue;
            session.Party.Keys = 3;

            var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session));

            Assert.Equal(3, loaded.Party.Keys);
            Assert.True(loaded.ActiveDungeon!.Maze.LockedDoorCount() > 0);
            return;
        }

        Assert.Fail("no seed produced a locked door to round-trip");
    }
}
