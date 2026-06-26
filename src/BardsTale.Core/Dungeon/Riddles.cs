namespace BardsTale.Core.Dungeon;

/// <summary>One riddle inscribed on a dungeon tile, with its accepted answers.</summary>
public sealed record Riddle(string Question, IReadOnlyList<string> Answers)
{
    /// <summary>True if the given answer matches (case-insensitive, trimmed, ignoring a leading "a/an/the").</summary>
    public bool Accepts(string answer)
    {
        var a = Normalize(answer);
        return Answers.Any(x => Normalize(x) == a);
    }

    private static string Normalize(string s)
    {
        var t = s.Trim().ToLowerInvariant();
        foreach (var article in new[] { "the ", "an ", "a " })
            if (t.StartsWith(article)) { t = t[article.Length..]; break; }
        return t;
    }
}

/// <summary>The catalogue of riddles a riddle tile can pose — timeless, fantasy-flavoured ones.</summary>
public static class Riddles
{
    public static readonly IReadOnlyList<Riddle> All = new[]
    {
        new Riddle("Voiceless it cries, wingless flutters, toothless bites, mouthless mutters. What is it?",
            new[] { "wind", "the wind" }),
        new Riddle("This thing all things devours: birds, beasts, trees, flowers; gnaws iron, bites steel, grinds hard stones to meal. What is it?",
            new[] { "time" }),
        new Riddle("I am not alive, but I grow; I have no lungs, but I need air; I have no mouth, but water slays me. What am I?",
            new[] { "fire", "flame" }),
        new Riddle("It cannot be seen, cannot be felt, cannot be heard, cannot be smelt; it lies behind stars and under hills, and empty holes it fills. What is it?",
            new[] { "dark", "darkness", "the dark" }),
        new Riddle("What has roots that nobody sees, is taller than trees, up it goes yet never grows?",
            new[] { "mountain", "a mountain" }),
        new Riddle("I follow you all the day, but at the dead of night I slip away. What am I?",
            new[] { "shadow", "your shadow" }),
        new Riddle("I speak without a mouth and hear without ears. I have no body, but I come alive with the wind. What am I?",
            new[] { "echo", "an echo" }),
        new Riddle("I have cities but no houses, forests but no trees, and rivers but no water. What am I?",
            new[] { "map", "a map" }),
        new Riddle("The more you take, the more you leave behind. What am I?",
            new[] { "footsteps", "steps", "footprints" }),
        new Riddle("I am tall when I am young and short when I am old; the longer I stand, the smaller I grow. What am I?",
            new[] { "candle", "a candle" }),
        new Riddle("What has a head and a tail but no body?",
            new[] { "coin", "a coin" }),
        new Riddle("What can fill a room yet takes up no space?",
            new[] { "light", "darkness", "sound" }),
    };

    public static Riddle Get(int id) => All[((id % All.Count) + All.Count) % All.Count];
}
