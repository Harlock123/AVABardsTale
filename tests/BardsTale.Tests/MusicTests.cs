using System;
using System.IO;
using System.Linq;
using BardsTale.UI.Audio;
using Xunit;

namespace BardsTale.Tests;

public class MusicTests
{
    [Theory]
    [InlineData(GameMusic.Town)]
    [InlineData(GameMusic.Dungeon)]
    [InlineData(GameMusic.Combat)]
    [InlineData(GameMusic.Victory)]
    [InlineData(GameMusic.DungeonDeep)]
    [InlineData(GameMusic.DungeonAbyss)]
    public void Each_track_renders_a_non_silent_loop_safe_buffer(GameMusic music)
    {
        var pcm = MusicSynth.BuildPcm(music);

        Assert.True(pcm.Length > MusicSynth.SampleRate, "loop should be at least ~1 second");
        Assert.Contains(pcm, s => Math.Abs((int)s) > 2000); // audible, not silence
        // Normalized below full-scale, so nothing should hard-clip.
        Assert.DoesNotContain(pcm, s => s == short.MaxValue || s == short.MinValue);
        // Faded ends → the loop point is silent, so repeats don't click.
        Assert.True(Math.Abs((int)pcm[0]) < 500);
        Assert.True(Math.Abs((int)pcm[^1]) < 500);

        // Auditioning hook: BT_DUMPMUSIC=1 writes the WAVs to TMPDIR.
        if (Environment.GetEnvironmentVariable("BT_DUMPMUSIC") == "1")
            File.WriteAllBytes(Path.Combine(Path.GetTempPath(), $"bt_{music}.wav"), MusicSynth.BuildWav(music));
    }

    [Fact]
    public void Tracks_are_distinct_from_one_another()
    {
        var tracks = Enum.GetValues<GameMusic>();
        var all = tracks.Select(MusicSynth.BuildPcm).ToList();
        // each pair differs in length or content
        for (var i = 0; i < all.Count; i++)
            for (var j = i + 1; j < all.Count; j++)
                Assert.False(all[i].SequenceEqual(all[j]),
                    $"{tracks[i]} and {tracks[j]} should be musically distinct");
    }

    [Theory]
    [InlineData(1, GameMusic.Dungeon)]
    [InlineData(6, GameMusic.Dungeon)]
    [InlineData(7, GameMusic.DungeonDeep)]
    [InlineData(13, GameMusic.DungeonDeep)]
    [InlineData(14, GameMusic.DungeonAbyss)]
    [InlineData(20, GameMusic.DungeonAbyss)]
    public void The_catacomb_ambience_darkens_with_depth(int depth, GameMusic expected)
        => Assert.Equal(expected, Music.DungeonTheme(depth));
}
