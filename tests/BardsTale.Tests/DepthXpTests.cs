using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Geometry;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

public class DepthXpTests
{
    private static GameState GameAtDepth(Party party, int depth)
    {
        var rng = new SystemRandomSource(seed: 3);
        var maze = new Maze("t", 5, 5) { StartPosition = new Position(2, 2), StartFacing = Direction.North };
        return new GameState(party, maze, rng, depth, new Position(2, 2), Direction.North);
    }

    private static long XpFromSameFight(int depth)
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        foreach (var m in party.Members) m.Experience = 0;
        var game = GameAtDepth(party, depth);
        game.ApplyVictory(new Encounter(new[] { new MonsterGroup(Bestiary.Skeleton, 1) }));
        return party.Members.First(m => !m.IsDead).Experience;
    }

    [Fact]
    public void The_same_fight_awards_far_more_xp_deeper_down()
    {
        var floor1 = XpFromSameFight(1);
        var floor10 = XpFromSameFight(10);
        var floor20 = XpFromSameFight(20);

        Assert.True(floor1 > 0);
        Assert.True(floor10 > floor1 * 3, "floor 10 should award several times the XP of floor 1");
        Assert.True(floor20 > floor10 * 3, "floor 20 should award several times the XP of floor 10");
    }

    [Fact]
    public void The_depth_multiplier_is_one_on_the_first_floor()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        Assert.Equal(1.0, GameAtDepth(party, 1).DepthXpScale, 3);
    }
}
