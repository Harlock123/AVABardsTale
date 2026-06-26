using System;
using System.Text;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Persistence;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Covers the seeded daily challenge: a deterministic daily seed, the run score, seed parsing,
/// that a seed reproduces an identical run (the whole point), and that the seed survives save/load.
/// </summary>
public class DailyChallengeTests
{
    [Fact]
    public void The_daily_seed_is_deterministic_and_date_specific()
    {
        var day = new DateOnly(2026, 6, 26);
        Assert.Equal(20260626, Challenges.DailySeed(day));
        Assert.NotEqual(Challenges.DailySeed(day), Challenges.DailySeed(day.AddDays(1)));
    }

    [Fact]
    public void The_score_rewards_depth_and_a_win()
    {
        var stats = new RunStats { DeepestDepth = 5, BattlesWon = 10, MonstersSlain = 20, GoldEarned = 1000 };
        Assert.Equal((5 - 1) * 1000 + 10 * 25 + 20 * 10 + 1000 / 10, stats.Score);

        var before = stats.Score;
        stats.Victory = true;
        Assert.Equal(before + 10000, stats.Score);

        Assert.True(new RunStats { DeepestDepth = 10 }.Score > new RunStats { DeepestDepth = 2 }.Score);
    }

    [Theory]
    [InlineData("12345", true, 12345)]
    [InlineData("  42 ", true, 42)]
    [InlineData("not-a-number", false, 0)]
    [InlineData("", false, 0)]
    public void Seed_parsing_accepts_only_whole_numbers(string text, bool ok, int expected)
    {
        Assert.Equal(ok, Challenges.TryParseSeed(text, out var seed));
        if (ok) Assert.Equal(expected, seed);
    }

    [Fact]
    public void The_same_seed_reproduces_the_same_party_and_dungeon()
    {
        var (partyA, mazeA) = SeededRun(7777);
        var (partyB, mazeB) = SeededRun(7777);
        var (_, mazeC) = SeededRun(8888);

        // Identical ready-made party.
        Assert.Equal(partyA.Members.Count, partyB.Members.Count);
        for (var i = 0; i < partyA.Members.Count; i++)
        {
            Assert.Equal(partyA.Members[i].MaxHitPoints, partyB.Members[i].MaxHitPoints);
            Assert.Equal(partyA.Members[i].Attributes.Strength, partyB.Members[i].Attributes.Strength);
        }

        // Identical dungeon for the same seed, but different for another.
        Assert.Equal(MazeSignature(mazeA), MazeSignature(mazeB));
        Assert.NotEqual(MazeSignature(mazeA), MazeSignature(mazeC));
    }

    [Fact]
    public void The_challenge_seed_survives_save_load()
    {
        var s = new GameSession(seed: 99) { ChallengeSeed = 20260626 };
        s.FillDefaultParty();

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(s));

        Assert.True(loaded.IsChallenge);
        Assert.Equal(20260626, loaded.ChallengeSeed);
    }

    private static (BardsTale.Core.Characters.Party Party, Maze Maze) SeededRun(int seed)
    {
        var s = new GameSession(seed);
        s.FillDefaultParty();
        return (s.Party, s.EnterDungeon().Maze);
    }

    private static string MazeSignature(Maze maze)
    {
        var sb = new StringBuilder();
        sb.Append(maze.StartPosition);
        for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
                sb.Append((int)maze[x, y].Walls).Append(':').Append((int)maze[x, y].Feature).Append(',');
        return sb.ToString();
    }
}
