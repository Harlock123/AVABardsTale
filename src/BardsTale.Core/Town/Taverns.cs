using BardsTale.Core.Util;

namespace BardsTale.Core.Town;

/// <summary>Rumours and gossip the party can buy a round to hear at Skara Brae's taverns.</summary>
public static class Taverns
{
    public const int RoundCost = 5;

    private static readonly IReadOnlyDictionary<string, string[]> RumorsByTavern =
        new Dictionary<string, string[]>
        {
            ["The Scarlet Bard"] = new[]
            {
                "\"Mad Wizard Mangar froze the town from his tower in the north...\"",
                "\"A Bard's song is worth a sword arm in the dark below.\"",
                "\"They say the catacombs go down further than any have returned from.\"",
                "\"Garth pays half-price for your old gear — robbery, if you ask me.\"",
                "\"Spiders in the cellars carry a poison that lingers. Buy an antidote, friend.\"",
            },
            ["Mad Mable's"] = new[]
            {
                "\"Mable waters the ale, but her rumours are strong enough.\"",
                "\"Ghouls down there can freeze a body stiff with a touch.\"",
                "\"The Coven Witches sing folk to sleep — wake your friends with a good shake.\"",
                "\"Some say the Mad God's acolytes can mend their own kind mid-fight.\"",
                "\"Rest at the Temple before you descend, or you'll not come back up.\"",
            },
        };

    public static IReadOnlyCollection<string> TavernNames => RumorsByTavern.Keys.ToArray();

    public static string RandomRumor(string tavernName, IRandomSource rng)
    {
        if (!RumorsByTavern.TryGetValue(tavernName, out var pool) || pool.Length == 0)
            return "The regulars have nothing to say tonight.";
        return rng.Pick(pool);
    }
}
