using System.Linq;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Lore;
using BardsTale.Core.Persistence;
using Xunit;

namespace BardsTale.Tests;

public class BestiaryTests
{
    private static Encounter Enc(params (MonsterTemplate t, int n)[] groups) =>
        new(groups.Select(g => new MonsterGroup(g.t, g.n)).ToList());

    [Fact]
    public void Facing_an_encounter_discovers_its_monsters_even_without_a_kill()
    {
        var codex = new MonsterCodex();
        codex.Discover(Enc((Bestiary.Skeleton, 2), (Bestiary.Kobold, 1)), depth: 1);

        Assert.True(codex.IsDiscovered("Skeleton"));
        Assert.True(codex.IsDiscovered("Kobold"));
        Assert.Equal(2, codex.DiscoveredCount);
        Assert.Equal(0, codex.For("Skeleton")!.Slain); // seen, not yet slain
    }

    [Fact]
    public void Recording_a_victory_tallies_kills_and_keeps_the_first_sighting()
    {
        var codex = new MonsterCodex();
        codex.RecordSlain(Enc((Bestiary.GiantSpider, 3)), depth: 2);

        var entry = codex.For("Giant Spider");
        Assert.NotNull(entry);
        Assert.Equal(3, entry!.Slain);
        Assert.Equal(2, entry.FirstSeenDepth);

        codex.RecordSlain(Enc((Bestiary.GiantSpider, 2)), depth: 5);
        Assert.Equal(5, codex.For("Giant Spider")!.Slain);
        Assert.Equal(2, codex.For("Giant Spider")!.FirstSeenDepth); // first sighting stays
    }

    [Fact]
    public void The_catalog_covers_wandering_monsters_and_bosses_without_duplicates()
    {
        var names = MonsterCatalog.All.Select(t => t.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.Contains("Skeleton", names);
        Assert.Contains("Crypt Tyrant", names);
        Assert.Contains("Mangar the Mad", names);
        Assert.Equal(MonsterCatalog.Count, new MonsterCodex().TotalCount);
    }

    [Fact]
    public void The_roster_spans_over_a_hundred_monsters_across_ten_tiers()
    {
        Assert.InRange(MonsterCatalog.Count, 112, 132);

        foreach (var tier in new[]
                 {
                     Bestiary.Tier1, Bestiary.Tier2, Bestiary.Tier3, Bestiary.Tier4, Bestiary.Tier5,
                     Bestiary.Tier6, Bestiary.Tier7, Bestiary.Tier8, Bestiary.Tier9, Bestiary.Tier10
                 })
            Assert.NotEmpty(tier);

        var names = MonsterCatalog.All.Select(t => t.Name).ToHashSet();
        foreach (var beast in new[]
                     {
                         "Goblin", "Orc", "Troll", "Werewolf", "Vampire", "Lich", "Beholder", "Mind Flayer",
                         "Ancient Red Dragon", "Tarrasque", "Demon Prince", "Balor", "Minotaur", "Basilisk"
                     })
            Assert.Contains(beast, names);

        Assert.Equal(names.Count, MonsterCatalog.Count); // no duplicate names anywhere
    }

    [Fact]
    public void Encounter_pools_get_tougher_with_depth()
    {
        var floor1 = Bestiary.PoolForDepth(1).Average(t => t.MaxHitPoints);
        var floor10 = Bestiary.PoolForDepth(10).Average(t => t.MaxHitPoints);
        var floor20 = Bestiary.PoolForDepth(20).Average(t => t.MaxHitPoints);

        Assert.True(floor1 < floor10, "floor 10 should be tougher than floor 1");
        Assert.True(floor10 < floor20, "floor 20 should be tougher than floor 10");
    }

    [Fact]
    public void The_codex_survives_a_save_load_round_trip()
    {
        var session = new GameSession(seed: 9);
        session.FillDefaultParty();
        session.Codex.RecordSlain(Enc((Bestiary.Wraith, 1)), depth: 3);
        session.Codex.Discover(Enc((Bestiary.Kobold, 2)), depth: 1);

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session));

        Assert.Equal(session.Codex.DiscoveredCount, loaded.Codex.DiscoveredCount);
        Assert.Equal(1, loaded.Codex.For("Wraith")!.Slain);
        Assert.Equal(3, loaded.Codex.For("Wraith")!.FirstSeenDepth);
        Assert.True(loaded.Codex.IsDiscovered("Kobold"));
        Assert.Equal(0, loaded.Codex.For("Kobold")!.Slain);
    }
}
