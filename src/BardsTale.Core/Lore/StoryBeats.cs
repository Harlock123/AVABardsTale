namespace BardsTale.Core.Lore;

/// <summary>A scripted story moment shown the first time the party reaches a given dungeon floor.</summary>
public sealed record StoryBeat(int Depth, string Title, string Text);

/// <summary>
/// A light main-quest thread: narrative beats that play out as the party descends, giving the
/// dungeon an arc from the descent beneath Skara Brae to the confrontation with Mangar.
/// </summary>
public static class StoryBeats
{
    public static readonly IReadOnlyList<StoryBeat> All = new[]
    {
        new StoryBeat(1, "Into the Catacombs",
            "Skara Brae lies frozen under an unnatural winter, and its people whisper one name: Mangar. " +
            "The Mad Wizard worked his cold from somewhere far below. You take up torch and blade and " +
            "descend into the catacombs beneath the city — the only road to his undoing."),

        new StoryBeat(3, "A Torn Journal Page",
            "Wedged in a skeleton's ribs is a water-stained page: \"...he speaks to the Mad God now, " +
            "and the dead rise at his word. I have seen the tower he raises in the deep. Heaven help us " +
            "if he finishes it.\" The hand trails off into a long dried smear."),

        new StoryBeat(5, "A Warning in the Dark",
            "A pale shade drifts from the wall and fixes you with hollow eyes. \"Turn back,\" it sighs. " +
            "\"Below, the cold has teeth. Mangar feeds it our souls.\" Then it unravels into mist, leaving " +
            "only a chill that the torches cannot warm."),

        new StoryBeat(8, "The Mad God's Mark",
            "The walls here are carved with a leering, many-eyed sigil, scrubbed in old blood. Cultists " +
            "of the Mad God have made these halls a temple, and they sing to something that answers. " +
            "Whatever Mangar serves, it is listening."),

        new StoryBeat(12, "The Deepening Cold",
            "Frost rimes every surface now, and your breath hangs in the air like smoke. This is the " +
            "wellspring of Skara Brae's winter — and it pours up from somewhere still deeper. You are " +
            "close enough to feel Mangar's power gnaw at your bones."),

        new StoryBeat(16, "The Tower's Shadow",
            "The catacombs open onto a vast frozen gulf, and across it rises a black spire that has no " +
            "business existing underground — Mangar's tower, grown from the living rock. His guardians " +
            "stir at your approach. There is no turning back from here."),

        new StoryBeat(20, "Mangar the Mad",
            "At the tower's heart, wreathed in killing frost, the Mad Wizard turns to face you. \"So the " +
            "city sends its little heroes,\" he sneers. \"Good. I have such a winter to show you.\" The " +
            "fate of Skara Brae comes down to this."),
    };

    /// <summary>The story beat that triggers on a given floor, or null if that floor has none.</summary>
    public static StoryBeat? ForDepth(int depth) => All.FirstOrDefault(b => b.Depth == depth);
}
