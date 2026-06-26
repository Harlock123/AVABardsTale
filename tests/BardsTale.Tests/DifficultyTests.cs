using BardsTale.Core.Combat;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Persistence;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Covers difficulty modes: the profile multipliers, how they scale a monster, that Normal is a
/// no-op, that harder runs pay out more, and that the chosen difficulty survives save/load.
/// </summary>
public class DifficultyTests
{
    [Fact]
    public void Profiles_scale_in_the_expected_directions()
    {
        var relaxed = DifficultyProfile.For(Difficulty.Relaxed);
        var hard = DifficultyProfile.For(Difficulty.Hard);

        Assert.True(relaxed.MonsterHp < 1.0 && hard.MonsterHp > 1.0);
        Assert.True(relaxed.EncounterChance < 1.0 && hard.EncounterChance > 1.0);
        Assert.True(relaxed.CampRisk < 1.0 && hard.CampRisk > 1.0);
        Assert.True(hard.Reward > 1.0);
        Assert.True(DifficultyProfile.Normal.IsBaseline);
    }

    [Fact]
    public void Scaling_a_monster_respects_difficulty()
    {
        var t = Bestiary.Ogre;

        var relaxed = NgPlus.Scale(t, 0, DifficultyProfile.For(Difficulty.Relaxed));
        var hard = NgPlus.Scale(t, 0, DifficultyProfile.For(Difficulty.Hard));

        Assert.True(relaxed.MaxHitPoints < t.MaxHitPoints, "Relaxed should weaken foes");
        Assert.True(hard.MaxHitPoints > t.MaxHitPoints, "Hard should toughen foes");
        Assert.True(hard.AttackBonus > t.AttackBonus, "Hard should hit harder");
    }

    [Fact]
    public void Normal_difficulty_is_a_no_op()
    {
        var t = Bestiary.Ogre;
        Assert.Equal(t, NgPlus.Scale(t, 0, DifficultyProfile.Normal));
        Assert.Equal(t, NgPlus.Scale(t, 0)); // default is Normal
    }

    [Fact]
    public void Harder_runs_pay_out_more_gold()
    {
        int GoldFor(Difficulty d)
        {
            var rng = new SystemRandomSource(seed: 3);
            var party = NewGame.CreateDefaultParty(rng);
            party.Gold = 0;
            var maze = new Maze("t", 3, 3);
            maze.SealBorders();
            var game = new GameState(party, maze, rng, ascension: 0, difficulty: DifficultyProfile.For(d));
            // A fixed, unscaled encounter so only the reward multiplier differs.
            var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Skeleton, 2) });
            game.ApplyVictory(encounter);
            return party.Gold;
        }

        var relaxed = GoldFor(Difficulty.Relaxed); // Reward 1.0
        var hard = GoldFor(Difficulty.Hard);       // Reward 1.2
        Assert.True(hard > relaxed, $"Hard ({hard}) should pay more gold than Relaxed ({relaxed})");
    }

    [Fact]
    public void Difficulty_survives_save_load()
    {
        var s = new GameSession(seed: 5);
        s.FillDefaultParty();
        s.Difficulty = Difficulty.Hard;

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(s));

        Assert.Equal(Difficulty.Hard, loaded.Difficulty);
    }

    [Fact]
    public void New_game_plus_keeps_the_chosen_difficulty()
    {
        var s = new GameSession(seed: 1) { Difficulty = Difficulty.Hard };
        s.FillDefaultParty();

        var ng = s.StartNewGamePlus();

        Assert.Equal(Difficulty.Hard, ng.Difficulty);
    }
}
