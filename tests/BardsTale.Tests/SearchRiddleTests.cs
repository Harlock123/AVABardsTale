using System.Linq;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Geometry;
using BardsTale.Core.Persistence;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

public class SearchRiddleTests
{
    private static (Position pos, Direction dir)? FindSecretDoor(Maze maze)
    {
        for (var x = 0; x < maze.Width; x++)
            for (var y = 0; y < maze.Height; y++)
            {
                var p = new Position(x, y);
                var secrets = maze.SecretDoorsAt(p);
                if (secrets.Count > 0) return (p, secrets[0]);
            }
        return null;
    }

    [Fact]
    public void Each_level_hides_secret_doors_and_a_riddle_tile()
    {
        var maze = new MazeBuilder(new SystemRandomSource(seed: 7)).Build("t", 11, 11);

        Assert.NotNull(FindSecretDoor(maze));
        Assert.NotNull(maze.PositionOf(CellFeature.Riddle));
    }

    [Fact]
    public void Searching_eventually_opens_a_secret_door()
    {
        var rng = new SystemRandomSource(seed: 7);
        var party = NewGame.CreateDefaultParty(rng);
        var maze = new MazeBuilder(rng).Build("t", 11, 11);
        var door = FindSecretDoor(maze)!.Value;

        var game = new GameState(party, maze, rng);
        party.Position = door.pos;

        Assert.False(maze.CanMove(door.pos, door.dir)); // a solid wall until found

        var found = false;
        for (var i = 0; i < 40 && !found; i++)
            found = game.Search().Found;

        Assert.True(found);
        Assert.True(maze.CanMove(door.pos, door.dir));         // the passage is open now
        Assert.Empty(maze.SecretDoorsAt(door.pos));            // no longer secret
    }

    [Fact]
    public void Searching_an_ordinary_cell_finds_nothing()
    {
        var rng = new SystemRandomSource(seed: 7);
        var party = NewGame.CreateDefaultParty(rng);
        var maze = new MazeBuilder(rng).Build("t", 11, 11);
        var game = new GameState(party, maze, rng);
        party.Position = maze.StartPosition; // the entrance has no secret door

        Assert.False(game.Search().Found);
    }

    [Fact]
    public void A_correct_riddle_answer_rewards_and_clears_the_tile()
    {
        var rng = new SystemRandomSource(seed: 3);
        var party = NewGame.CreateDefaultParty(rng);
        var maze = new MazeBuilder(rng).Build("t", 9, 9);
        var game = new GameState(party, maze, rng);
        game.CurrentCell.Feature = CellFeature.Riddle;
        game.CurrentCell.RiddleId = 0;
        var riddle = Riddles.Get(0);

        // A wrong answer leaves the tile for another try.
        Assert.False(game.AnswerRiddle("not the answer").Correct);
        Assert.Equal(CellFeature.Riddle, game.CurrentCell.Feature);

        var goldBefore = party.Gold;
        var result = game.AnswerRiddle(riddle.Answers[0]);

        Assert.True(result.Correct);
        Assert.True(party.Gold > goldBefore);
        Assert.Equal(CellFeature.None, game.CurrentCell.Feature); // solved, runes go dark
    }

    [Fact]
    public void Riddle_answers_ignore_case_articles_and_whitespace()
    {
        var riddle = new Riddle("?", new[] { "map" });
        Assert.True(riddle.Accepts("MAP"));
        Assert.True(riddle.Accepts("  a map "));
        Assert.True(riddle.Accepts("the map"));
        Assert.False(riddle.Accepts("globe"));
    }

    [Fact]
    public void Secret_doors_and_riddles_survive_save_load()
    {
        var session = new GameSession(seed: 9);
        session.FillDefaultParty();
        var maze = session.EnterDungeon().Maze;

        var door = FindSecretDoor(maze)!.Value;
        var riddlePos = maze.PositionOf(CellFeature.Riddle)!.Value;
        var riddleId = maze[riddlePos].RiddleId;

        var loadedMaze = GameSerializer.FromJson(GameSerializer.ToJson(session)).ActiveDungeon!.Maze;

        Assert.NotEqual(Walls.None, loadedMaze[door.pos].SecretDoors);
        Assert.Equal(CellFeature.Riddle, loadedMaze[riddlePos].Feature);
        Assert.Equal(riddleId, loadedMaze[riddlePos].RiddleId);
    }
}
