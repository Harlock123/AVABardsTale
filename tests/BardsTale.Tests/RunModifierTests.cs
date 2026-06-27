using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Persistence;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

/// <summary>Covers run modifiers (mutators): the effect helpers, in-game application, score and save/load.</summary>
public class RunModifierTests
{
    private static GameState WithModifier(RunModifier mod, out Party party, int seed = 3)
    {
        var rng = new SystemRandomSource(seed);
        party = NewGame.CreateDefaultParty(rng);
        var maze = new Maze("t", 3, 3);
        maze.SealBorders();
        return new GameState(party, maze, rng, ascension: 0, difficulty: DifficultyProfile.Normal, modifiers: mod);
    }

    [Fact]
    public void The_effect_helpers_read_the_flags()
    {
        Assert.True(RunModifiers.EncounterChance(RunModifier.Relentless) > 1.0);
        Assert.True(RunModifiers.Reward(RunModifier.Pauper) < 1.0);
        Assert.False(RunModifiers.ShopsOpen(RunModifier.NoShops));
        Assert.False(RunModifiers.CampAllowed(RunModifier.NoCamp));
        Assert.True(RunModifiers.LootUnidentified(RunModifier.Cursed));

        Assert.Equal(2, RunModifiers.Count(RunModifier.NoShops | RunModifier.Relentless));
        Assert.Equal(1.4, RunModifiers.ScoreMultiplier(RunModifier.NoShops | RunModifier.Relentless), 3);
    }

    [Fact]
    public void No_camp_refuses_to_rest()
    {
        var game = WithModifier(RunModifier.NoCamp, out _);
        Assert.False(game.CanCamp);

        var result = game.Camp();

        Assert.False(result.Rested);
        Assert.Null(result.Ambush);
        Assert.Contains(result.Log, l => l.Contains("no resting"));
    }

    [Fact]
    public void Pauper_pays_out_less_than_a_plain_run()
    {
        int GoldFor(RunModifier mod)
        {
            var game = WithModifier(mod, out var party);
            party.Gold = 0;
            game.ApplyVictory(new Encounter(new[] { new MonsterGroup(Bestiary.Skeleton, 4) }));
            return party.Gold;
        }

        Assert.True(GoldFor(RunModifier.Pauper) < GoldFor(RunModifier.None));
    }

    [Fact]
    public void Modifiers_survive_save_load()
    {
        var s = new GameSession(seed: 5) { Modifiers = RunModifier.NoShops | RunModifier.Cursed };
        s.FillDefaultParty();

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(s));

        Assert.Equal(RunModifier.NoShops | RunModifier.Cursed, loaded.Modifiers);
        Assert.False(loaded.ShopsOpen);
    }
}
