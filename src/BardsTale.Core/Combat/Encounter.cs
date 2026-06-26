using BardsTale.Core.Util;

namespace BardsTale.Core.Combat;

/// <summary>A cluster of identical monsters that fight as one targetable group.</summary>
public sealed class MonsterGroup
{
    private readonly List<Monster> _monsters;

    public MonsterGroup(MonsterTemplate template, int count)
    {
        Template = template;
        _monsters = Enumerable.Range(0, count).Select(_ => Monster.Spawn(template)).ToList();
    }

    public MonsterTemplate Template { get; }
    public IReadOnlyList<Monster> Monsters => _monsters;
    public int LivingCount => _monsters.Count(m => !m.IsDead);
    public bool IsDefeated => LivingCount == 0;

    public string Name => Template.Name;

    public Monster? FirstAlive() => _monsters.FirstOrDefault(m => !m.IsDead);

    /// <summary>Removes a monster that has routed — it leaves the field entirely (no XP, no loot).</summary>
    public bool Remove(Monster monster) => _monsters.Remove(monster);
}

/// <summary>A whole encounter: up to a few monster groups facing the party.</summary>
public sealed class Encounter
{
    /// <summary>Hard cap on groups so summoners can't grow a fight without bound.</summary>
    public const int MaxGroups = 6;

    private readonly List<MonsterGroup> _groups;

    public Encounter(IReadOnlyList<MonsterGroup> groups, bool isBoss = false, bool isFinalBoss = false)
    {
        _groups = groups.ToList();
        IsBoss = isBoss;
        IsFinalBoss = isFinalBoss;
    }

    /// <summary>True for the fixed lair fights, which are tougher and cleared permanently.</summary>
    public bool IsBoss { get; }

    /// <summary>True for the climactic fight with Mangar — winning it wins the game.</summary>
    public bool IsFinalBoss { get; }

    public IReadOnlyList<MonsterGroup> Groups => _groups;
    public bool IsCleared => _groups.All(g => g.IsDefeated);
    public IEnumerable<MonsterGroup> LivingGroups => _groups.Where(g => !g.IsDefeated);
    public bool CanSummonMore => _groups.Count < MaxGroups;

    /// <summary>True when a buffed elite leads this pack — they drop richer loot.</summary>
    public bool HasElite => _groups.Any(g => g.Template.IsElite);

    public int TotalExperience => _groups.Sum(g => g.Monsters.Count * g.Template.ExperienceValue);
    public int TotalGold => _groups.Sum(g => g.Monsters.Count * g.Template.GoldValue);

    /// <summary>Adds a fresh group of reinforcements (up to the group cap) and returns it.</summary>
    public MonsterGroup? AddReinforcements(MonsterTemplate template, int count)
    {
        if (!CanSummonMore) return null;
        var group = new MonsterGroup(template, count);
        _groups.Add(group);
        return group;
    }
}

/// <summary>Rolls random encounters for the dungeon's wandering monsters.</summary>
public sealed class EncounterFactory
{
    private readonly IRandomSource _rng;

    public EncounterFactory(IRandomSource rng) => _rng = rng;

    public Encounter CreateRandom(int depth)
    {
        var pool = Bestiary.PoolForDepth(depth);
        var groupCount = _rng.Next(1, depth >= 3 ? 3 : 4);
        var groups = new List<MonsterGroup>();
        for (var i = 0; i < groupCount; i++)
        {
            var template = _rng.Pick(pool);
            var count = _rng.Next(1, template.MaxPerGroup + 1);
            groups.Add(new MonsterGroup(template, count));
        }

        // A lone elite occasionally leads the pack — buffed, and worth far more.
        if (_rng.Chance(Elites.ChanceForDepth(depth)))
            groups.Insert(0, new MonsterGroup(Elites.Promote(_rng.Pick(pool)), 1));

        return new Encounter(groups);
    }
}
