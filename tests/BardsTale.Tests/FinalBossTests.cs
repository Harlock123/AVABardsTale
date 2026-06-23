using System;
using System.IO;
using BardsTale.Core.Combat;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Geometry;
using BardsTale.Core.Items;
using BardsTale.Core.Util;
using BardsTale.UI.Services;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

public class FinalBossTests
{
    [Fact]
    public void The_final_depth_lair_is_mangar_and_wins_the_game()
    {
        var encounter = Bosses.Create(Bosses.FinalDepth);

        Assert.True(encounter.IsBoss);
        Assert.True(encounter.IsFinalBoss);
        Assert.Equal(Bosses.Mangar, encounter.Groups[0].Template);
        Assert.Equal(Bosses.Mangar, Bosses.BossForDepth(Bosses.FinalDepth));
        Assert.NotEqual(Bosses.Mangar, Bosses.BossForDepth(1)); // earlier levels have lesser bosses
    }

    [Fact]
    public void Defeating_mangar_raises_the_game_won_event()
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

        var maze = new Maze("t", 5, 5) { StartPosition = new Position(2, 2), StartFacing = Direction.North };
        maze[2, 1].Feature = CellFeature.BossLair;
        var game = new GameState(party, maze, rng, depth: Bosses.FinalDepth,
            position: new Position(2, 2), facing: Direction.North);

        var exploration = new ExplorationViewModel(game);
        var won = false;
        exploration.GameWonRequested += () => won = true;

        exploration.MoveForwardCommand.Execute(null); // step into Mangar's lair
        Assert.True(exploration.IsInCombat);

        var safety = 0;
        while (exploration.Combat is not null && safety++ < 300)
            exploration.Combat.AutoCommand.Execute(null);

        Assert.True(won);
    }

    [Fact]
    public void Starting_a_new_game_returns_to_a_fresh_town()
    {
        var dir = Path.Combine(Path.GetTempPath(), "BardsTaleTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var main = new MainWindowViewModel(new SaveService(dir));
            main.NewGameCommand.Execute(null);

            Assert.False(main.IsGameWon);
            Assert.IsType<TownViewModel>(main.CurrentView);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
