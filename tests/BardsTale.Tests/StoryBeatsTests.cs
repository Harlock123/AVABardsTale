using BardsTale.Core.Game;
using BardsTale.Core.Lore;
using BardsTale.Core.Persistence;
using Xunit;

namespace BardsTale.Tests;

/// <summary>Covers the light main-quest thread: the story beats and their play-once tracking.</summary>
public class StoryBeatsTests
{
    [Fact]
    public void Key_floors_have_a_beat_and_others_do_not()
    {
        Assert.NotNull(StoryBeats.ForDepth(1));   // the descent begins
        Assert.NotNull(StoryBeats.ForDepth(20));  // Mangar
        Assert.Null(StoryBeats.ForDepth(2));      // a quiet floor

        Assert.All(StoryBeats.All, b =>
        {
            Assert.False(string.IsNullOrWhiteSpace(b.Title));
            Assert.False(string.IsNullOrWhiteSpace(b.Text));
        });
    }

    [Fact]
    public void The_final_beat_introduces_Mangar()
        => Assert.Contains("Mangar", StoryBeats.ForDepth(20)!.Title);

    [Fact]
    public void A_beat_plays_only_once()
    {
        var session = new GameSession(seed: 1);

        Assert.NotNull(session.ReachStory(1)); // first time on floor 1 — it plays
        Assert.Null(session.ReachStory(1));    // revisiting — it doesn't
        Assert.Null(session.ReachStory(2));    // a floor with no beat
    }

    [Fact]
    public void Story_progress_survives_save_load()
    {
        var session = new GameSession(seed: 1);
        session.FillDefaultParty();
        Assert.NotNull(session.ReachStory(1)); // mark floor 1 seen

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session));

        Assert.Null(loaded.ReachStory(1)); // still seen after a round-trip — it won't replay
    }
}
