using System.Collections.Generic;
using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Geometry;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

public class SpecialTileTests
{
    // Party starts at (2,2) facing North; the tile under test sits one step ahead at (2,1).
    private static (GameState game, Party party) StepOnto(CellFeature feature, Position? destination = null)
    {
        var rng = new SystemRandomSource(seed: 1);
        var party = NewGame.CreateDefaultParty(rng);
        var maze = new Maze("t", 5, 5) { StartPosition = new Position(2, 2), StartFacing = Direction.North };
        maze[2, 1].Feature = feature;
        maze[2, 1].Destination = destination;
        return (new GameState(party, maze, rng), party);
    }

    [Fact]
    public void A_spinner_randomises_the_partys_facing()
    {
        var (game, party) = StepOnto(CellFeature.SpinnerTrap);
        var result = game.StepForward();

        Assert.Equal(MoveResultKind.Spun, result.Kind);
        Assert.Equal(new Position(2, 1), party.Position); // you still step onto it
    }

    [Fact]
    public void A_teleporter_whisks_the_party_to_its_destination()
    {
        var (game, party) = StepOnto(CellFeature.Teleporter, destination: new Position(0, 4));
        var result = game.StepForward();

        Assert.Equal(MoveResultKind.Teleported, result.Kind);
        Assert.Equal(new Position(0, 4), party.Position);
    }

    [Fact]
    public void A_trap_wounds_the_party()
    {
        var (game, party) = StepOnto(CellFeature.Trap);
        var before = party.Members.Sum(m => m.HitPoints);

        var result = game.StepForward();

        Assert.Equal(MoveResultKind.Trapped, result.Kind);
        Assert.True(party.Members.Sum(m => m.HitPoints) < before);
    }

    [Fact]
    public void Darkness_cannot_be_mapped()
    {
        var (game, _) = StepOnto(CellFeature.Darkness);
        var result = game.StepForward();

        Assert.Equal(MoveResultKind.Darkness, result.Kind);
        Assert.False(game.Maze[2, 1].Visited); // you can't record what you can't see
    }

    [Fact]
    public void Generated_levels_contain_special_tiles_with_a_wired_teleporter()
    {
        var maze = new MazeBuilder(new SystemRandomSource(seed: 42)).Build("t", 16, 16);

        var features = new HashSet<CellFeature>();
        Cell? teleporter = null;
        for (var x = 0; x < maze.Width; x++)
            for (var y = 0; y < maze.Height; y++)
            {
                features.Add(maze[x, y].Feature);
                if (maze[x, y].Feature == CellFeature.Teleporter)
                    teleporter = maze[x, y];
            }

        Assert.Contains(CellFeature.SpinnerTrap, features);
        Assert.Contains(CellFeature.Trap, features);
        Assert.Contains(CellFeature.Darkness, features);
        Assert.NotNull(teleporter);
        Assert.NotNull(teleporter!.Destination);
    }
}
