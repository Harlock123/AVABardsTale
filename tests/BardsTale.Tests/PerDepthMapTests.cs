using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Geometry;
using BardsTale.Core.Persistence;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

public class PerDepthMapTests
{
    private static GameState NewDungeon(out Maze level1)
    {
        var rng = new SystemRandomSource(seed: 1);
        var party = NewGame.CreateDefaultParty(rng);
        level1 = new Maze("Level 1", 6, 6) { StartPosition = new Position(3, 3), StartFacing = Direction.North };
        return new GameState(party, level1, rng);
    }

    [Fact]
    public void Cannot_ascend_from_the_entrance_level()
    {
        var game = NewDungeon(out _);
        Assert.False(game.CanAscend);

        game.Descend();
        Assert.True(game.CanAscend);
    }

    [Fact]
    public void Ascending_returns_to_the_same_upper_level_with_its_map_intact()
    {
        var game = NewDungeon(out var level1);

        game.Descend();
        Assert.Equal(2, game.Depth);
        Assert.NotSame(level1, game.Maze); // a freshly carved level

        game.Ascend();
        Assert.Equal(1, game.Depth);
        Assert.Same(level1, game.Maze); // the very same maze object — explored map preserved
    }

    [Fact]
    public void Re_descending_restores_the_previously_generated_level()
    {
        var game = NewDungeon(out _);

        game.Descend();
        var level2 = game.Maze;

        game.Ascend();
        game.Descend();

        Assert.Same(level2, game.Maze); // not regenerated — the same level 2 we explored before
    }

    [Fact]
    public void Every_explored_level_survives_a_save_round_trip()
    {
        var session = new GameSession(seed: 1);
        session.FillDefaultParty();
        var game = session.EnterDungeon();
        game.Maze[0, 0].Visited = true;   // a distinctive footprint on level 1
        game.Descend();
        game.Maze[0, 0].Visited = true;   // and one on level 2
        Assert.Equal(2, game.Levels.Count);

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session));
        var dungeon = loaded.ActiveDungeon!;

        Assert.Equal(2, dungeon.Depth);
        Assert.Equal(2, dungeon.Levels.Count);
        Assert.True(dungeon.Levels[1][0, 0].Visited);
        Assert.True(dungeon.Levels[2][0, 0].Visited);
    }
}
