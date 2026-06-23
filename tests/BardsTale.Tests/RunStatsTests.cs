using System.Linq;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Geometry;
using BardsTale.Core.Items;
using BardsTale.Core.Persistence;
using BardsTale.Core.Util;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

public class RunStatsTests
{
    [Fact]
    public void Winning_a_fight_tallies_battles_monsters_and_gold()
    {
        var rng = new SystemRandomSource(seed: 1);
        var party = NewGame.CreateDefaultParty(rng);
        foreach (var m in party.Members) // an unstoppable party so the fight resolves to victory
        {
            m.Level = 20;
            m.MaxHitPoints = 9999;
            m.HitPoints = 9999;
            m.Weapon = Items.Enchant(Items.BattleAxe, 3);
        }

        // A depth-1 boss lair: Skeleton Lord + 3 Skeletons = 4 monsters in one battle.
        var maze = new Maze("t", 5, 5) { StartPosition = new Position(2, 2), StartFacing = Direction.North };
        maze[2, 1].Feature = CellFeature.BossLair;
        var game = new GameState(party, maze, rng);
        var stats = new RunStats();
        var exploration = new ExplorationViewModel(game, stats);

        exploration.MoveForwardCommand.Execute(null);
        var safety = 0;
        while (exploration.Combat is not null && safety++ < 300)
            exploration.Combat.AutoCommand.Execute(null);

        Assert.Equal(1, stats.BattlesWon);
        Assert.Equal(4, stats.MonstersSlain);
        Assert.True(stats.GoldEarned > 0);
    }

    [Fact]
    public void Descending_records_the_deepest_level()
    {
        var rng = new SystemRandomSource(seed: 1);
        var party = NewGame.CreateDefaultParty(rng);
        var maze = new Maze("t", 5, 5) { StartPosition = new Position(2, 2), StartFacing = Direction.North };
        maze[2, 2].Feature = CellFeature.StairsDown; // standing on the stair from the off
        var game = new GameState(party, maze, rng);
        var stats = new RunStats();
        var exploration = new ExplorationViewModel(game, stats);

        exploration.DescendCommand.Execute(null);

        Assert.Equal(2, stats.DeepestDepth);
    }

    [Fact]
    public void Run_stats_survive_a_save_round_trip()
    {
        var session = new GameSession(seed: 1);
        session.FillDefaultParty();
        session.Stats.BattlesWon = 7;
        session.Stats.MonstersSlain = 42;
        session.Stats.GoldEarned = 1234;
        session.Stats.DeepestDepth = 3;

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session));

        Assert.Equal(7, loaded.Stats.BattlesWon);
        Assert.Equal(42, loaded.Stats.MonstersSlain);
        Assert.Equal(1234, loaded.Stats.GoldEarned);
        Assert.Equal(3, loaded.Stats.DeepestDepth);
    }
}
