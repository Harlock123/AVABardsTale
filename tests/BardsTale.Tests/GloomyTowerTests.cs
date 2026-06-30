using System.Linq;
using BardsTale.Core.Combat;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Persistence;
using BardsTale.Core.Town;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// The Gloomy Tower — a second, optional dungeon entered from the town centre, independent of the
/// catacombs: its own boss band, its own map-stack and depth, and full save/load support.
/// </summary>
public class GloomyTowerTests
{
    [Fact]
    public void The_town_has_a_central_tower_entrance()
    {
        var town = TownMapBuilder.Build();
        var tower = town.Buildings.FirstOrDefault(b => b.Building == TownBuilding.TowerEntrance);
        Assert.NotNull(tower);
        // It sits near the centre (the spawn is 5,5), not out at a corner like the catacomb stair.
        Assert.True(System.Math.Abs(tower!.Position.X - 5) <= 1 && System.Math.Abs(tower.Position.Y - 5) <= 1);
    }

    [Fact]
    public void The_tower_has_its_own_boss_band_topped_by_the_gloomlord()
    {
        Assert.Equal(Bosses.Gloomlord, Bosses.BossForDepth(DungeonKind.Tower, Bosses.TowerDepth));
        // Its lower floors differ from the catacombs' opening boss.
        Assert.NotEqual(Bosses.SkeletonLord, Bosses.BossForDepth(DungeonKind.Tower, 1));
        Assert.Equal(8, Bosses.FinalDepthOf(DungeonKind.Tower));
        Assert.Equal(20, Bosses.FinalDepthOf(DungeonKind.Catacombs));
    }

    [Fact]
    public void Clearing_the_tower_apex_does_not_win_the_game()
    {
        var apex = Bosses.Create(DungeonKind.Tower, Bosses.TowerDepth);
        Assert.True(apex.IsBoss);
        Assert.False(apex.IsFinalBoss); // only Mangar in the catacombs wins the game
        Assert.Equal(Bosses.Gloomlord, apex.Groups[0].Template);
    }

    [Fact]
    public void The_two_dungeons_are_independent()
    {
        var session = new GameSession(seed: 5);
        session.FillDefaultParty();

        var cata = session.EnterDungeon();
        var tower = session.EnterTower();

        Assert.NotSame(cata, tower);
        Assert.Equal(DungeonKind.Catacombs, cata.Kind);
        Assert.Equal(DungeonKind.Tower, tower.Kind);
        Assert.Contains("Gloomy Tower", tower.Maze.Name);
        Assert.Contains("Catacombs", cata.Maze.Name);

        // Re-entering each resumes the same instance, not a fresh one.
        Assert.Same(cata, session.EnterDungeon());
        Assert.Same(tower, session.EnterTower());
    }

    [Fact]
    public void The_tower_caps_descent_at_its_top_floor()
    {
        var session = new GameSession(seed: 5);
        session.FillDefaultParty();
        var tower = session.EnterTower();

        for (var i = 0; i < 20; i++) tower.Descend();
        Assert.Equal(Bosses.TowerDepth, tower.Depth); // never past the apex floor
    }

    [Fact]
    public void The_tower_draws_wandering_foes_from_its_own_curated_pool()
    {
        // The tower's pool is the arcane/undead roster, NOT the catacombs' early vermin.
        var towerNames = Enumerable.Range(1, Bosses.TowerDepth)
            .SelectMany(d => Bestiary.PoolForTower(d))
            .Select(t => t.Name).ToHashSet();

        Assert.Contains("Wraith", towerNames);
        Assert.Contains("Iron Golem", towerNames);
        Assert.Contains("Eye Tyrant", towerNames);
        // Catacomb entry-vermin never wander the tower.
        Assert.DoesNotContain("Giant Rat", towerNames);
        Assert.DoesNotContain("Kobold", towerNames);

        // A factory keyed to the tower never rolls those vermin, even on its first floor.
        var factory = new EncounterFactory(new BardsTale.Core.Util.SystemRandomSource(1), kind: DungeonKind.Tower);
        for (var i = 0; i < 60; i++)
            foreach (var g in factory.CreateRandom(1).Groups)
                Assert.DoesNotContain("Rat", g.Template.BaseName);
    }

    [Fact]
    public void Both_dungeons_survive_save_and_load()
    {
        var session = new GameSession(seed: 5);
        session.FillDefaultParty();
        session.EnterDungeon().Descend(); // catacombs at depth 2
        var tower = session.EnterTower();
        tower.Descend();
        tower.Descend(); // tower at floor 3

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session));

        Assert.NotNull(loaded.ActiveDungeon);
        Assert.NotNull(loaded.ActiveTower);
        Assert.Equal(DungeonKind.Catacombs, loaded.ActiveDungeon!.Kind);
        Assert.Equal(DungeonKind.Tower, loaded.ActiveTower!.Kind);
        Assert.Equal(3, loaded.ActiveTower.Depth);
        Assert.Contains("Gloomy Tower", loaded.ActiveTower.Maze.Name);
    }
}
