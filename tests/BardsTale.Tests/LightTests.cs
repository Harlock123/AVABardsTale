using BardsTale.Core.Characters;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Geometry;
using BardsTale.Core.Util;
using BardsTale.Desktop.ViewModels;
using Xunit;

namespace BardsTale.Tests;

public class LightTests
{
    private static GameState OpenLevel(out Party party, CellFeature ahead = CellFeature.None)
    {
        var rng = new SystemRandomSource(seed: 1);
        party = NewGame.CreateDefaultParty(rng);
        var maze = new Maze("t", 5, 5) { StartPosition = new Position(2, 2), StartFacing = Direction.North };
        maze[2, 1].Feature = ahead;
        return new GameState(party, maze, rng);
    }

    [Fact]
    public void Light_lets_the_party_see_and_map_darkness()
    {
        var game = OpenLevel(out _, CellFeature.Darkness);
        game.GrantLight(GameState.LightDurationSteps);

        var result = game.StepForward();

        Assert.True(game.HasLight);
        Assert.NotEqual(MoveResultKind.Darkness, result.Kind); // not blacked out
        Assert.True(game.Maze[2, 1].Visited);                  // mapped while lit
    }

    [Fact]
    public void Without_light_darkness_is_blinding()
    {
        var game = OpenLevel(out _, CellFeature.Darkness);
        var result = game.StepForward();

        Assert.False(game.HasLight);
        Assert.Equal(MoveResultKind.Darkness, result.Kind);
        Assert.False(game.Maze[2, 1].Visited);
    }

    [Fact]
    public void Light_burns_down_one_step_at_a_time()
    {
        var game = OpenLevel(out _);
        game.GrantLight(1);
        Assert.True(game.HasLight);

        game.StepForward();
        Assert.False(game.HasLight); // a single step exhausted the one-step light
    }

    [Fact]
    public void Casting_light_while_exploring_turns_it_on()
    {
        var game = NewGame.CreateDefault(seed: 5); // default party includes a Bard who knows a light song
        var vm = new ExplorationViewModel(game);

        Assert.False(vm.HasLight);
        vm.CastLightCommand.Execute(null);
        Assert.True(vm.HasLight);
    }
}
