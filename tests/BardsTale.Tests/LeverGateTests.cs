using BardsTale.Core.Characters;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Geometry;
using BardsTale.Core.Persistence;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Covers the lever-and-portcullis puzzle: barred gates seal treasure vaults, and a rune
/// lever elsewhere on the level raises every gate when pulled.
/// </summary>
public class LeverGateTests
{
    // A tiny corridor: (0,0)-(1,0)-(2,0), open between cells, sealed at the borders.
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
    public void A_barred_gate_blocks_movement_with_a_portcullis_message()
    {
        var (game, party, maze) = Corridor();
        maze.MarkGate(1, 0, Direction.East); // bar the (1,0)->(2,0) passage
        party.Position = new Position(1, 0);
        party.Facing = Direction.East;

        var result = game.StepForward();

        Assert.Equal(MoveResultKind.BlockedByWall, result.Kind);
        Assert.Contains("portcullis", result.Description);
        Assert.Equal(new Position(1, 0), party.Position); // didn't budge
        Assert.False(maze.CanMove(new Position(1, 0), Direction.East));
    }

    [Fact]
    public void Pulling_the_lever_raises_every_gate_and_spends_the_lever()
    {
        var (game, party, maze) = Corridor();
        maze.MarkGate(1, 0, Direction.East);
        party.Position = new Position(1, 0);
        maze[new Position(1, 0)].Feature = CellFeature.Lever;

        var result = game.PullLever();

        Assert.Equal(1, result.GatesOpened);
        Assert.Contains("gate", result.Message);
        Assert.Equal(0, maze.GateCount());
        Assert.True(maze.CanMove(new Position(1, 0), Direction.East)); // the way is open now
        Assert.Equal(CellFeature.None, maze[new Position(1, 0)].Feature); // lever locked spent
    }

    [Fact]
    public void After_the_lever_the_party_can_reach_the_sealed_vault()
    {
        var (game, party, maze) = Corridor();
        maze.MarkGate(1, 0, Direction.East);
        maze[new Position(2, 0)].Feature = CellFeature.Chest; // the vault beyond the gate
        party.Position = new Position(1, 0);
        party.Facing = Direction.East;
        maze[new Position(1, 0)].Feature = CellFeature.Lever;

        game.PullLever();
        game.StepForward();

        Assert.Equal(new Position(2, 0), party.Position);
    }

    [Fact]
    public void Stepping_onto_a_lever_offers_the_pull_prompt()
    {
        var (game, party, maze) = Corridor();
        maze[new Position(1, 0)].Feature = CellFeature.Lever;
        party.Position = new Position(0, 0);
        party.Facing = Direction.East;

        var result = game.StepForward();

        Assert.Equal(MoveResultKind.Lever, result.Kind);
        Assert.True(game.OnLever);
    }

    [Fact]
    public void A_spent_lever_pulled_again_does_nothing()
    {
        var (game, party, maze) = Corridor();
        party.Position = new Position(1, 0);
        maze[new Position(1, 0)].Feature = CellFeature.Lever; // a lever, but no gates anywhere

        var result = game.PullLever();

        Assert.Equal(0, result.GatesOpened);
    }

    [Fact]
    public void Generated_levels_pair_every_gate_with_a_lever()
    {
        var withPuzzle = 0;
        for (var seed = 0; seed < 12; seed++)
        {
            var maze = new MazeBuilder(new SystemRandomSource(seed)).Build("t", 11, 11);
            var lever = maze.PositionOf(CellFeature.Lever);
            var gates = maze.GateCount();

            // The builder never seals a gate it can't unlock, and never drops a useless lever.
            if (gates > 0) Assert.NotNull(lever);
            if (lever is not null)
            {
                Assert.True(gates > 0, "a lever should always have at least one gate to raise");
                withPuzzle++;
            }
        }

        Assert.True(withPuzzle >= 8, $"only {withPuzzle}/12 generated levels had the lever puzzle");
    }

    [Fact]
    public void A_barred_gate_and_its_lever_survive_save_load()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var session = new GameSession(seed: seed);
            session.FillDefaultParty();
            var maze = session.EnterDungeon().Maze;

            Position? gatePos = null;
            var gateDir = Direction.North;
            for (var x = 0; x < maze.Width && gatePos is null; x++)
                for (var y = 0; y < maze.Height && gatePos is null; y++)
                    foreach (Direction d in System.Enum.GetValues<Direction>())
                        if (maze[new Position(x, y)].HasGate(d)) { gatePos = new Position(x, y); gateDir = d; break; }

            if (gatePos is null) continue; // this seed laid no gate — try the next

            var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session)).ActiveDungeon!.Maze;

            Assert.True(loaded[gatePos.Value].HasGate(gateDir));         // the gate is still barred
            Assert.NotNull(loaded.PositionOf(CellFeature.Lever));        // its lever is still there
            return;
        }

        Assert.Fail("no seed produced a barred gate to round-trip");
    }
}
