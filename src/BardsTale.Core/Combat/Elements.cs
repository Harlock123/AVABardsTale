namespace BardsTale.Core.Combat;

/// <summary>A damage element. Weapons deal <see cref="Physical"/>; spells and powers vary.</summary>
[Flags]
public enum Element
{
    None = 0,
    Physical = 1 << 0,
    Fire = 1 << 1,
    Cold = 1 << 2,
    Lightning = 1 << 3,
    Poison = 1 << 4,
    Arcane = 1 << 5
}

/// <summary>
/// Each monster's elemental affinities — what it resists (takes half), what it is weak to
/// (takes double), and what it is wholly immune to (takes none — e.g. fire to a salamander).
/// Kept as one table keyed by name so the 120-strong bestiary doesn't need editing; lots of
/// foes are simply neutral.
/// </summary>
public static class MonsterElements
{
    private readonly record struct Affinity(Element Resist, Element Weak, Element Immune = Element.None);

    private const Element UndeadResist = Element.Poison | Element.Cold;
    private const Element IncorporealResist = Element.Physical | Element.Cold;

    private static readonly Dictionary<string, Affinity> Table = new()
    {
        // --- Undead: cold and poison do little; fire (and holy arcane) sears them ---
        ["Skeleton"] = new(UndeadResist, Element.Fire),
        ["Zombie"] = new(Element.Poison, Element.Fire),
        ["Ghoul"] = new(UndeadResist, Element.Fire),
        ["Ghast"] = new(UndeadResist, Element.Fire),
        ["Wight"] = new(Element.Cold, Element.Fire),
        ["Vampire"] = new(Element.Cold, Element.Fire),
        ["Elder Vampire"] = new(Element.Cold, Element.Fire),
        ["Nightwalker"] = new(UndeadResist, Element.Fire),
        ["Death Knight"] = new(Element.Cold, Element.Fire),
        ["Lich"] = new(UndeadResist, Element.Fire),
        ["Elder Lich"] = new(UndeadResist, Element.Fire | Element.Arcane),
        ["Bone Golem"] = new(Element.Physical | Element.Poison, Element.Lightning),

        // --- Incorporeal: shrug off blades, but raw arcane force unbinds them ---
        ["Shadow"] = new(Element.Physical, Element.Arcane),
        ["Wraith"] = new(IncorporealResist, Element.Arcane),
        ["Wraith Lord"] = new(IncorporealResist, Element.Arcane),
        ["Banshee"] = new(Element.Physical, Element.Arcane),
        ["Will-o-Wisp"] = new(Element.Physical, Element.Arcane, Immune: Element.Lightning),

        // --- Fire-blooded: bathe in flame (utterly immune), flinch at frost ---
        ["Salamander"] = new(Element.None, Element.Cold, Immune: Element.Fire),
        ["Flame Shade"] = new(Element.None, Element.Cold, Immune: Element.Fire),
        ["Hell Hound"] = new(Element.None, Element.Cold, Immune: Element.Fire),
        ["Pit Fiend"] = new(Element.None, Element.Cold, Immune: Element.Fire),
        ["Balor"] = new(Element.None, Element.Cold, Immune: Element.Fire),
        ["Demon Prince"] = new(Element.None, Element.Cold, Immune: Element.Fire),
        ["Arch-Devil"] = new(Element.None, Element.Cold, Immune: Element.Fire),
        ["Young Red Dragon"] = new(Element.None, Element.Cold, Immune: Element.Fire),
        ["Ancient Red Dragon"] = new(Element.None, Element.Cold, Immune: Element.Fire),

        // --- Frost-born: immune to cold, melt to fire ---
        ["Frost Giant"] = new(Element.None, Element.Fire, Immune: Element.Cold),
        ["Adult White Dragon"] = new(Element.None, Element.Fire, Immune: Element.Cold),

        // --- Storm/energy: lightning passes harmlessly through them ---
        ["Spark Imp"] = new(Element.None, Element.Cold, Immune: Element.Lightning),
        ["Storm Drake"] = new(Element.None, Element.Cold, Immune: Element.Lightning),
        ["Storm Giant"] = new(Element.None, Element.Cold, Immune: Element.Lightning),
        ["Young Blue Dragon"] = new(Element.None, Element.Cold, Immune: Element.Lightning),
        ["Eye Tyrant"] = new(Element.Arcane, Element.Physical),

        // --- Vermin & acid: poison runs in their veins; fire and cold undo them ---
        ["Giant Spider"] = new(Element.Poison, Element.Fire),
        ["Giant Scorpion"] = new(Element.Poison, Element.Fire),
        ["Giant Centipede"] = new(Element.Poison, Element.Fire),
        ["Giant Ant"] = new(Element.Poison, Element.Fire),
        ["Giant Wasp"] = new(Element.Poison, Element.Fire),
        ["Phase Spider"] = new(Element.Poison, Element.Fire),
        ["Ettercap"] = new(Element.Poison, Element.Fire),
        ["Basilisk"] = new(Element.Poison, Element.Fire),
        ["Gray Ooze"] = new(Element.Poison | Element.Cold, Element.Fire),
        ["Wyvern"] = new(Element.Poison, Element.Cold),
        ["Hydra"] = new(Element.Poison, Element.Fire),
        ["Ancient Black Dragon"] = new(Element.Poison, Element.Cold),

        // --- Constructs: blades and poison barely register; lightning shorts them ---
        ["Stone Golem"] = new(Element.Physical | Element.Poison, Element.Lightning),
        ["Iron Golem"] = new(Element.Physical | Element.Poison | Element.Fire, Element.Lightning),
        ["Animated Armor"] = new(Element.Poison, Element.Lightning),
        ["Gargoyle"] = new(Element.Physical, Element.Arcane),

        // --- Trolls regenerate — only fire stops them for good ---
        ["Troll"] = new(Element.None, Element.Fire),

        // --- Bosses ---
        ["Skeleton Lord"] = new(UndeadResist, Element.Fire),
        ["Coven Matron"] = new(Element.None, Element.Arcane),
        ["Crypt Tyrant"] = new(UndeadResist, Element.Fire),
        ["Demon Lord"] = new(Element.None, Element.Cold, Immune: Element.Fire),
        ["Troll King"] = new(Element.None, Element.Fire),
        ["Vampire Lord"] = new(Element.Cold, Element.Fire),
        ["Stone Titan"] = new(Element.Physical, Element.Lightning),
        ["Lich King"] = new(UndeadResist, Element.Fire),
        ["Wyvern Matriarch"] = new(Element.Poison, Element.Cold),
        ["Beholder Tyrant"] = new(Element.Arcane, Element.Physical),
        ["Frost King"] = new(Element.None, Element.Fire, Immune: Element.Cold),
        ["Death Tyrant"] = new(UndeadResist, Element.Fire),
        ["Pit Lord"] = new(Element.None, Element.Cold, Immune: Element.Fire),
        ["Archlich"] = new(UndeadResist, Element.Fire | Element.Arcane),
        ["Dragon Tyrant"] = new(Element.None, Element.Cold, Immune: Element.Fire),
        ["Mangar the Mad"] = new(Element.Arcane, Element.None),

        // --- The chest-lurker: its wooden hide catches fire easily ---
        ["Mimic"] = new(Element.None, Element.Fire),
    };

    // Elites share their base creature's affinities ("Elite Skeleton" → "Skeleton").
    private static string Strip(string name) =>
        name.StartsWith("Elite ", StringComparison.Ordinal) ? name["Elite ".Length..] : name;

    public static Element ResistOf(string name) => Table.TryGetValue(Strip(name), out var a) ? a.Resist : Element.None;
    public static Element WeakOf(string name) => Table.TryGetValue(Strip(name), out var a) ? a.Weak : Element.None;

    /// <summary>What a creature is utterly immune to — magic of that element does nothing at all to it.</summary>
    public static Element ImmuneOf(string name) => Table.TryGetValue(Strip(name), out var a) ? a.Immune : Element.None;

    /// <summary>
    /// The element of a monster's damaging spell or breath weapon, keyed by name so the
    /// roster's <c>MonsterSpell</c> definitions need no editing. Anything unlisted (mind
    /// blasts, soul bolts, disintegration) counts as raw <see cref="Element.Arcane"/>.
    /// </summary>
    private static readonly Dictionary<string, Element> SpellTable = new()
    {
        // Fire
        ["Cinderblast"] = Element.Fire,
        ["Fire Breath"] = Element.Fire,
        ["Fire Whip"] = Element.Fire,
        ["Hellfire"] = Element.Fire,
        ["Hellstorm"] = Element.Fire,
        ["Inferno"] = Element.Fire,
        ["Cataclysm"] = Element.Fire,
        ["Cataclysm Breath"] = Element.Fire,
        // Cold
        ["Frost Breath"] = Element.Cold,
        ["Blizzard"] = Element.Cold,
        // Lightning
        ["Spark"] = Element.Lightning,
        ["Lightning Breath"] = Element.Lightning,
        ["Thunderclap"] = Element.Lightning,
        // Poison / acid / necrotic
        ["Acid Breath"] = Element.Poison,
        ["Necrotic Breath"] = Element.Poison,
    };

    /// <summary>The element of a monster spell by name (Arcane when not specifically themed).</summary>
    public static Element OfSpell(string name) => SpellTable.TryGetValue(name, out var e) ? e : Element.Arcane;

    /// <summary>Renders an element set as a readable list ("Fire, Cold"), or "" if none.</summary>
    public static string Describe(Element elements)
    {
        if (elements == Element.None) return "";
        var parts = new List<string>();
        foreach (Element e in Enum.GetValues<Element>())
            if (e != Element.None && elements.HasFlag(e))
                parts.Add(e.ToString());
        return string.Join(", ", parts);
    }

    /// <summary>A single evocative glyph for one element (used for compact affinity/ward badges).</summary>
    public static string Glyph(Element e) => e switch
    {
        Element.Physical => "⚔",
        Element.Fire => "🔥",
        Element.Cold => "❄",
        Element.Lightning => "⚡",
        Element.Poison => "☠",
        Element.Arcane => "✦",
        _ => ""
    };

    /// <summary>Renders an element set as a row of glyphs ("🔥 ❄"), or "" if none.</summary>
    public static string DescribeGlyphs(Element elements)
    {
        if (elements == Element.None) return "";
        var parts = new List<string>();
        foreach (Element e in Enum.GetValues<Element>())
            if (e != Element.None && elements.HasFlag(e))
                parts.Add(Glyph(e));
        return string.Join(" ", parts);
    }
}
