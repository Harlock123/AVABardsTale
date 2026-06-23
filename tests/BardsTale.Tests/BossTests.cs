using BardsTale.Core.Combat;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Geometry;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

public class BossTests
{
    [Fact]
    public void A_lair_encounter_is_a_boss_with_minions_and_rich_rewards()
    {
        var encounter = Bosses.Create(1);

        Assert.True(encounter.IsBoss);
        Assert.True(encounter.Groups.Count >= 2); // boss plus a minion group
        Assert.Equal(Bosses.SkeletonLord, encounter.Groups[0].Template);
        Assert.True(encounter.TotalExperience > 400);
    }

    [Fact]
    public void The_boss_changes_with_depth_ending_in_mangar()
    {
        Assert.Equal(Bosses.SkeletonLord, Bosses.BossForDepth(1));
        Assert.Equal(Bosses.CovenMatron, Bosses.BossForDepth(2));
        Assert.Equal(Bosses.Mangar, Bosses.BossForDepth(Bosses.FinalDepth)); // the final boss guards the depths
    }

    [Fact]
    public void Generated_levels_contain_a_boss_lair()
    {
        var maze = new MazeBuilder(new SystemRandomSource(seed: 42)).Build("t", 16, 16);

        var found = false;
        for (var x = 0; x < maze.Width && !found; x++)
            for (var y = 0; y < maze.Height; y++)
                if (maze[x, y].Feature == CellFeature.BossLair) { found = true; break; }

        Assert.True(found);
    }

    [Fact]
    public void Stepping_into_a_lair_triggers_the_boss_then_clears_once_beaten()
    {
        var rng = new SystemRandomSource(seed: 1);
        var party = NewGame.CreateDefaultParty(rng);
        var maze = new Maze("t", 5, 5) { StartPosition = new Position(2, 2), StartFacing = Direction.North };
        maze[2, 1].Feature = CellFeature.BossLair;
        var game = new GameState(party, maze, rng);

        var result = game.StepForward();
        Assert.Equal(MoveResultKind.Encounter, result.Kind);
        Assert.NotNull(result.Encounter);
        Assert.True(result.Encounter!.IsBoss);

        game.ClearBoss();
        Assert.Equal(CellFeature.None, game.Maze[2, 1].Feature);
    }
}
