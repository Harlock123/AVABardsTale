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
        var all = new[] { GameMusic.Town, GameMusic.Dungeon, GameMusic.Combat, GameMusic.Victory }
            .Select(MusicSynth.BuildPcm)
            .ToList();
        // each pair differs in length or content
        for (var i = 0; i < all.Count; i++)
            for (var j = i + 1; j < all.Count; j++)
                Assert.False(all[i].SequenceEqual(all[j]), "tracks should be musically distinct");
    }
}
