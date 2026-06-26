using BardsTale.Core.Combat;
using BardsTale.Core.Util;

namespace BardsTale.Core.Town;

/// <summary>
/// A snapshot of the party's progress used to colour tavern rumours with real, useful hints —
/// the boss lurking on the next floor down, what prowls there, and how many secrets remain.
/// </summary>
public sealed record RumorContext(
    int DeepestDepth,
    int SecretDoors,
    int Riddles,
    int Gates,
    int LockedDoors,
    int Keys);

/// <summary>Rumours and gossip the party can buy a round to hear at Skara Brae's taverns.</summary>
public static class Taverns
{
    public const int RoundCost = 5;

    /// <summary>How often a bought round yields a useful, progress-aware tip rather than pure flavour.</summary>
    private const double DynamicChance = 0.6;

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

    /// <summary>Pure-flavour rumour (no game context) — kept for callers that have no progress to draw on.</summary>
    public static string RandomRumor(string tavernName, IRandomSource rng) => Rumor(tavernName, rng, null);

    /// <summary>
    /// A rumour from <paramref name="tavernName"/>. With a game <paramref name="ctx"/> the round
    /// usually yields a real tip — the next floor's boss, a creature that haunts it, or a hint at
    /// the secrets and locked vaults still waiting below — otherwise it's the house's usual gossip.
    /// </summary>
    public static string Rumor(string tavernName, IRandomSource rng, RumorContext? ctx)
    {
        RumorsByTavern.TryGetValue(tavernName, out var flavour);
        flavour ??= System.Array.Empty<string>();

        if (ctx is not null)
        {
            var tips = DynamicRumors(rng, ctx);
            if (tips.Count > 0 && (flavour.Length == 0 || rng.Chance(DynamicChance)))
                return rng.Pick(tips);
        }

        return flavour.Length > 0 ? rng.Pick(flavour) : "The regulars have nothing to say tonight.";
    }

    /// <summary>Builds the list of progress-aware tips that currently apply.</summary>
    private static List<string> DynamicRumors(IRandomSource rng, RumorContext ctx)
    {
        var tips = new List<string>();
        var nextDepth = Math.Clamp(ctx.DeepestDepth + 1, 1, Bosses.FinalDepth);

        // The boss waiting on the next floor down (or the master himself, at the bottom).
        if (nextDepth >= Bosses.FinalDepth)
            tips.Add("\"Mangar the Mad himself broods at the very bottom of the catacombs. Few dare speak of going so deep.\"");
        else
            tips.Add($"\"They say a {Bosses.BossForDepth(nextDepth).Name} lairs on the {Ordinal(nextDepth)} level down — come ready for it.\"");

        // Something that prowls the next tier — drawn from the real monster pool for that depth.
        var threat = rng.Pick(Bestiary.PoolForDepth(nextDepth)).Name;
        tips.Add($"\"Travellers back from the {Ordinal(nextDepth)} level swear {Article(threat)} {threat} stalks the dark down there.\"");

        // Hints at the puzzles and treasure still unclaimed in the explored catacombs.
        if (ctx.SecretDoors > 0)
            tips.Add("\"There are passages down there no one's found yet — tap the walls and search, friend.\"");
        if (ctx.Riddles > 0)
            tips.Add("\"A riddle graven in the stone below pays out handsomely for a clever tongue.\"");
        if (ctx.Gates > 0)
            tips.Add("\"A barred vault waits in the deep — somewhere there's a lever that raises the gate.\"");
        if (ctx.LockedDoors > 0 && ctx.Keys <= 0)
            tips.Add("\"A locked door guards treasure below, and its iron key lies dropped somewhere on the same floor.\"");
        if (ctx.LockedDoors > 0 && ctx.Keys > 0)
            tips.Add("\"Carrying an iron key, are you? There's a locked door below it was cut for.\"");

        return tips;
    }

    private static string Ordinal(int n)
    {
        var suffix = n % 100 is >= 11 and <= 13
            ? "th"
            : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{n}{suffix}";
    }

    private static string Article(string word) =>
        word.Length > 0 && "AEIOUaeiou".IndexOf(word[0]) >= 0 ? "an" : "a";
}
