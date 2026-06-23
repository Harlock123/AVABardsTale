using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Game;
using BardsTale.Core.Geometry;
using BardsTale.Core.Items;
using BardsTale.Core.Persistence;
using Xunit;

namespace BardsTale.Tests;

public class PersistenceTests
{
    private static GameSession BuildRichSession()
    {
        var session = new GameSession(seed: 5);
        session.FillDefaultParty();
        session.Party.Gold = 1234;
        session.Party.Inventory.Add(Items.HealingPotion);
        session.Party.Inventory.Add(Items.Antidote);
        session.TownPosition = new Position(3, 4);
        session.TownFacing = Direction.East;

        var hero = session.Party.Members[0];
        hero.Experience = 555;
        hero.HitPoints = 7;
        hero.Inflict(StatusEffect.Poisoned);
        hero.Weapon = Items.BattleAxe;

        var dungeon = session.EnterDungeon();
        dungeon.TurnRight();
        dungeon.StepForward();
        dungeon.Descend(); // now on a deeper level
        dungeon.GrantLight(30);
        return session;
    }

    [Fact]
    public void Round_trip_preserves_party_gold_inventory_and_town_position()
    {
        var session = BuildRichSession();
        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session));

        Assert.Equal(1234, loaded.Party.Gold);
        Assert.Equal(session.Party.Members.Count, loaded.Party.Members.Count);
        Assert.Equal(new Position(3, 4), loaded.TownPosition);
        Assert.Equal(Direction.East, loaded.TownFacing);
        Assert.Contains(Items.HealingPotion, loaded.Party.Inventory);
        Assert.Contains(Items.Antidote, loaded.Party.Inventory);
    }

    [Fact]
    public void Round_trip_preserves_character_detail()
    {
        var session = BuildRichSession();
        var original = session.Party.Members[0];
        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session)).Party.Members[0];

        Assert.Equal(original.Name, loaded.Name);
        Assert.Equal(original.Race, loaded.Race);
        Assert.Equal(original.Class, loaded.Class);
        Assert.Equal(555, loaded.Experience);
        Assert.Equal(7, loaded.HitPoints);
        Assert.True(loaded.IsPoisoned);
        Assert.Equal(Items.BattleAxe, loaded.Weapon);
        Assert.Equal(original.Attributes.Strength, loaded.Attributes.Strength);
        Assert.Equal(original.KnownSpells, loaded.KnownSpells);
        Assert.Equal(original.KnownSongs, loaded.KnownSongs);
    }

    [Fact]
    public void Round_trip_preserves_the_explored_dungeon()
    {
        var session = BuildRichSession();
        var dungeon = session.ActiveDungeon!;
        var visitedBefore = CountVisited(dungeon);

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session));

        Assert.NotNull(loaded.ActiveDungeon);
        var restored = loaded.ActiveDungeon!;
        Assert.Equal(dungeon.Depth, restored.Depth);
        Assert.Equal(30, restored.LightRemaining);
        Assert.Equal(dungeon.Maze.Width, restored.Maze.Width);
        Assert.Equal(visitedBefore, CountVisited(restored));
        Assert.Equal(WallSignature(dungeon), WallSignature(restored));
    }

    [Fact]
    public void Round_trip_preserves_special_tiles_and_teleporter_destinations()
    {
        var session = BuildRichSession();
        var maze = session.ActiveDungeon!.Maze;

        BardsTale.Core.Geometry.Position? teleAt = null;
        BardsTale.Core.Geometry.Position? teleDest = null;
        for (var x = 0; x < maze.Width && teleAt is null; x++)
            for (var y = 0; y < maze.Height; y++)
                if (maze[x, y].Feature == BardsTale.Core.Dungeon.CellFeature.Teleporter)
                {
                    teleAt = new BardsTale.Core.Geometry.Position(x, y);
                    teleDest = maze[x, y].Destination;
                    break;
                }
        Assert.NotNull(teleAt);

        var restored = GameSerializer.FromJson(GameSerializer.ToJson(session)).ActiveDungeon!.Maze;
        var cell = restored[teleAt!.Value.X, teleAt.Value.Y];
        Assert.Equal(BardsTale.Core.Dungeon.CellFeature.Teleporter, cell.Feature);
        Assert.Equal(teleDest, cell.Destination);
    }

    [Fact]
    public void A_session_with_no_dungeon_round_trips_without_one()
    {
        var session = new GameSession(seed: 1);
        session.FillDefaultParty();

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session));
        Assert.Null(loaded.ActiveDungeon);
        Assert.Equal(6, loaded.Party.Members.Count);
    }

    private static int CountVisited(GameState dungeon)
    {
        var count = 0;
        for (var x = 0; x < dungeon.Maze.Width; x++)
            for (var y = 0; y < dungeon.Maze.Height; y++)
                if (dungeon.Maze[x, y].Visited)
                    count++;
        return count;
    }

    private static string WallSignature(GameState dungeon)
        => string.Concat(Enumerable.Range(0, dungeon.Maze.Width * dungeon.Maze.Height)
            .Select(i => (int)dungeon.Maze[i % dungeon.Maze.Width, i / dungeon.Maze.Width].Walls)
            .Select(w => w.ToString("X")));
}
