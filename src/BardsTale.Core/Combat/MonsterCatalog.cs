namespace BardsTale.Core.Combat;

/// <summary>
/// The complete roster of monsters the bestiary tracks — the wandering foes plus
/// the fixed lair bosses — in a stable reading order with duplicates removed.
/// </summary>
public static class MonsterCatalog
{
    public static IReadOnlyList<MonsterTemplate> All { get; } = Build();

    public static int Count => All.Count;

    private static IReadOnlyList<MonsterTemplate> Build()
    {
        var seen = new HashSet<string>();
        var list = new List<MonsterTemplate>();
        void Add(MonsterTemplate t)
        {
            if (seen.Add(t.Name)) list.Add(t);
        }

        foreach (var t in Bestiary.AllWandering) Add(t);
        foreach (var boss in Bosses.All) Add(boss);
        return list;
    }

    public static MonsterTemplate? Find(string name) =>
        All.FirstOrDefault(t => t.Name == name);
}
