using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Geometry;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

public class GeometryTests
{
    [Theory]
    [InlineData(Direction.North, Direction.East)]
    [InlineData(Direction.East, Direction.South)]
    [InlineData(Direction.West, Direction.North)]
    public void TurnRight_advances_clockwise(Direction start, Direction expected)
        => Assert.Equal(expected, start.TurnRight());

    [Fact]
    public void Opposite_is_involutive()
    {
        foreach (Direction d in Enum.GetValues<Direction>())
            Assert.Equal(d, d.Opposite().Opposite());
    }

    [Fact]
    public void North_step_decreases_Y()
        => Assert.Equal(new Position(3, 2), new Position(3, 3).Step(Direction.North));
}

public class MazeTests
{
    [Fact]
    public void SetWall_is_symmetric_for_both_cells()
    {
        var maze = new Maze("t", 4, 4);
        maze.SetWall(1, 1, Direction.East);
        Assert.True(maze[1, 1].HasWall(Direction.East));
        Assert.True(maze[2, 1].HasWall(Direction.West));
    }

    [Fact]
    public void Generated_maze_is_fully_connected()
    {
        var rng = new SystemRandomSource(seed: 42);
        var maze = new MazeBuilder(rng).Build("t", 12, 12);

        var seen = new HashSet<Position>();
        var stack = new Stack<Position>();
        stack.Push(new Position(0, 0));
        while (stack.Count > 0)
        {
            var p = stack.Pop();
            if (!seen.Add(p)) continue;
            foreach (Direction d in Enum.GetValues<Direction>())
                if (maze.CanMove(p, d))
                    stack.Push(p.Step(d));
        }

        Assert.Equal(maze.Width * maze.Height, seen.Count);
    }
}

public class CharacterTests
{
    [Fact]
    public void Factory_produces_valid_attributes_and_health()
    {
        var rng = new SystemRandomSource(seed: 1);
        var c = new CharacterFactory(rng).Create("Test", Race.Human, CharacterClass.Warrior);

        Assert.InRange(c.Attributes.Strength, 3, 18);
        Assert.True(c.MaxHitPoints >= 1);
        Assert.Equal(c.MaxHitPoints, c.HitPoints);
        Assert.NotNull(c.Weapon);
    }

    [Fact]
    public void Spellcaster_starts_with_spells_and_points()
    {
        var rng = new SystemRandomSource(seed: 7);
        var c = new CharacterFactory(rng).Create("Mage", Race.Elf, CharacterClass.Magician);
        Assert.True(c.IsSpellcaster);
        Assert.NotEmpty(c.KnownSpells);
        Assert.True(c.MaxSpellPoints >= 1);
    }

    [Fact]
    public void Damage_beyond_health_marks_dead()
    {
        var rng = new SystemRandomSource(seed: 2);
        var c = new CharacterFactory(rng).Create("Test", Race.Human, CharacterClass.Rogue);
        c.ApplyDamage(c.MaxHitPoints + 50);
        Assert.True(c.IsDead);
        Assert.Equal(0, c.HitPoints);
    }
}

public class CombatTests
{
    [Fact]
    public void Party_eventually_defeats_a_lone_weak_monster()
    {
        var rng = new SystemRandomSource(seed: 99);
        var party = NewGame.CreateDefaultParty(rng);
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) });
        var engine = new CombatEngine(party, encounter, rng, surprise: SurpriseState.None);

        var safety = 0;
        while (!engine.IsOver && safety++ < 100)
        {
            var commands = party.Members
                .Where(m => m.CanAct)
                .Select(m => new CombatCommand(m, CombatActionType.Attack, 0))
                .ToList();
            engine.ExecuteRound(commands);
        }

        Assert.True(encounter.IsCleared);
        Assert.False(party.IsWiped);
    }

    [Fact]
    public void Victory_awards_experience_to_living_members()
    {
        var rng = new SystemRandomSource(seed: 5);
        var game = NewGame.CreateDefault(seed: 5);
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Skeleton, 2) });

        game.ApplyVictory(encounter);

        Assert.All(game.Party.Members, m => Assert.True(m.Experience > 0));
    }
}
