using BardsTale.Core.Combat;

namespace BardsTale.Core.Lore;

/// <summary>What the party has learned about a single monster.</summary>
public sealed class CodexEntry
{
    public string Name { get; set; } = "";
    public int Slain { get; set; }
    public int FirstSeenDepth { get; set; } = 1;
}

/// <summary>
/// The party's bestiary: which monsters they have faced and how many they've slain.
/// An entry appears the moment a fight begins (so fleeing still records the foe), and
/// its kill tally grows with each victory. Persisted as part of the save.
/// </summary>
public sealed class MonsterCodex
{
    private readonly Dictionary<string, CodexEntry> _entries = new();

    public IReadOnlyDictionary<string, CodexEntry> Entries => _entries;

    public int DiscoveredCount => _entries.Count;
    public int TotalCount => MonsterCatalog.Count;

    public bool IsDiscovered(string name) => _entries.ContainsKey(name);
    public CodexEntry? For(string name) => _entries.GetValueOrDefault(name);

    /// <summary>Records every monster in an encounter as seen — call when a fight begins.</summary>
    public void Discover(Encounter encounter, int depth)
    {
        // Elites fold into their base creature's entry (an Elite Goblin counts as a Goblin).
        foreach (var group in encounter.Groups)
            DiscoverOne(group.Template.BaseName, depth);
    }

    /// <summary>Marks a single monster as seen, remembering the depth it was first met.</summary>
    public void DiscoverOne(string name, int depth)
    {
        if (!_entries.ContainsKey(name))
            _entries[name] = new CodexEntry { Name = name, FirstSeenDepth = Math.Max(1, depth) };
    }

    /// <summary>Tallies the monsters slain in a won fight (and ensures they're discovered).</summary>
    public void RecordSlain(Encounter encounter, int depth)
    {
        foreach (var group in encounter.Groups)
        {
            var name = group.Template.BaseName;
            DiscoverOne(name, depth);
            _entries[name].Slain += group.Monsters.Count;
        }
    }

    /// <summary>Reinstates an entry from a saved game.</summary>
    public void Restore(CodexEntry entry) => _entries[entry.Name] = entry;
}
