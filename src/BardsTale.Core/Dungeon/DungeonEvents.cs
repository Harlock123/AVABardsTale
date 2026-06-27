namespace BardsTale.Core.Dungeon;

/// <summary>What resolving a dungeon-event choice does. The narrative lives in the catalogue;
/// the actual effect is applied by <see cref="Game.GameState.ResolveEvent"/>.</summary>
public enum EventEffect
{
    /// <summary>Walk away — nothing happens.</summary>
    Leave,
    /// <summary>A windfall of gold, scaled by depth.</summary>
    FindGold,
    /// <summary>Lose some gold (a trap, a thief, a toll).</summary>
    LoseGold,
    /// <summary>Restore a share of the party's HP and spell points.</summary>
    Heal,
    /// <summary>Hurt the whole party (a snare, a curse).</summary>
    Harm,
    /// <summary>Turn up a magic item (and a little gold).</summary>
    FindItem,
    /// <summary>Stake gold on a coin-flip — win double or lose it all.</summary>
    Wager,
    /// <summary>A random good turn — gold, healing, or treasure.</summary>
    Boon,
    /// <summary>A random ill turn — harm or stolen gold.</summary>
    Curse,
    /// <summary>The fickle hand of fate — an even chance of a boon or a curse.</summary>
    Fate
}

/// <summary>One choice the party can make at a dungeon event.</summary>
public sealed record EventOption(string Label, EventEffect Effect, int Magnitude = 0, int Cost = 0);

/// <summary>A non-combat dungeon encounter: a scene and a few choices, each with an outcome.</summary>
public sealed record DungeonEvent(string Id, string Title, string Prompt, IReadOnlyList<EventOption> Options);

/// <summary>The catalogue of dungeon events scattered between the fights.</summary>
public static class DungeonEvents
{
    public static readonly IReadOnlyList<DungeonEvent> All = new[]
    {
        new DungeonEvent("well", "A Wishing Well",
            "A still, dark pool glints with sunken coins, and the air hums with a faint old magic.",
            new[]
            {
                new EventOption("Drop in a coin and make a wish (20 gold)", EventEffect.Boon, Cost: 20),
                new EventOption("Drink deeply from the cool water", EventEffect.Heal, Magnitude: 45),
                new EventOption("Leave it be", EventEffect.Leave),
            }),

        new DungeonEvent("merchant", "A Trapped Merchant",
            "A merchant lies pinned beneath fallen rubble, reaching out a trembling, hopeful hand.",
            new[]
            {
                new EventOption("Heave the rubble aside and free them", EventEffect.Boon),
                new EventOption("Rifle through their scattered cart instead", EventEffect.FindGold, Magnitude: 8),
                new EventOption("Walk on", EventEffect.Leave),
            }),

        new DungeonEvent("imp", "A Gambling Imp",
            "A grinning imp rattles a cup of knucklebones. \"Care to test your luck, mortal? Double or nothing!\"",
            new[]
            {
                new EventOption("Wager 50 gold", EventEffect.Wager, Magnitude: 50),
                new EventOption("Wager 150 gold", EventEffect.Wager, Magnitude: 150),
                new EventOption("Refuse and move on", EventEffect.Leave),
            }),

        new DungeonEvent("shrine", "A Crumbling Shrine",
            "A worn altar to some forgotten god stands in an alcove. Old power lingers in the cold stone.",
            new[]
            {
                new EventOption("Kneel and pray", EventEffect.Fate),
                new EventOption("Smash it open for its relics", EventEffect.FindItem),
                new EventOption("Leave it undisturbed", EventEffect.Leave),
            }),

        new DungeonEvent("camp", "An Abandoned Camp",
            "Embers still glow in a long-cold campsite; scattered bedrolls and a guttered lantern remain.",
            new[]
            {
                new EventOption("Rest a while by the embers", EventEffect.Heal, Magnitude: 35),
                new EventOption("Scavenge the camp", EventEffect.FindGold, Magnitude: 6),
                new EventOption("Press on", EventEffect.Leave),
            }),
    };

    public static DungeonEvent Get(int id) => All[Math.Clamp(id, 0, All.Count - 1)];
    public static int Count => All.Count;
}
