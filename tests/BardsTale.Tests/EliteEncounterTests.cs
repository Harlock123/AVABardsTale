using System.Linq;
using BardsTale.Core.Combat;
using BardsTale.Core.Items;
using BardsTale.Core.Lore;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

public class EliteEncounterTests
{
    [Fact]
    public void Promoting_a_monster_buffs_its_stats_and_worth()
    {
        var basic = Bestiary.Goblin;
        var elite = Elites.Promote(basic);

        Assert.True(elite.IsElite);
        Assert.Equal("Elite Goblin", elite.Name);
        Assert.Equal("Goblin", elite.BaseName);
        Assert.True(elite.MaxHitPoints > basic.MaxHitPoints);
        Assert.True(elite.ExperienceValue > basic.ExperienceValue);
        Assert.True(elite.GoldValue > basic.GoldValue);
        Assert.Equal(1, elite.MaxPerGroup);
    }

    [Fact]
    public void An_elite_keeps_its_base_creatures_elemental_affinity()
    {
        // Skeletons are weak to fire; an Elite Skeleton should be too.
        Assert.True((MonsterElements.WeakOf("Elite Skeleton") & Element.Fire) != 0);
        Assert.True((MonsterElements.ResistOf("Elite Skeleton") & Element.Poison) != 0);
    }

    [Fact]
    public void An_elite_is_catalogued_under_its_base_creature()
    {
        var codex = new MonsterCodex();
        var encounter = new Encounter(new[] { new MonsterGroup(Elites.Promote(Bestiary.Goblin), 1) });

        codex.Discover(encounter, depth: 3);

        Assert.True(codex.IsDiscovered("Goblin"));
        Assert.False(codex.IsDiscovered("Elite Goblin")); // no separate, count-inflating entry
        Assert.Equal(1, codex.DiscoveredCount);
    }

    [Fact]
    public void Slaying_an_elite_tallies_against_the_base_creature()
    {
        var codex = new MonsterCodex();
        var encounter = new Encounter(new[] { new MonsterGroup(Elites.Promote(Bestiary.Orc), 1) });

        codex.RecordSlain(encounter, depth: 5);

        Assert.Equal(1, codex.For("Orc")!.Slain);
    }

    [Fact]
    public void An_encounter_flags_when_an_elite_leads_it()
    {
        var withElite = new Encounter(new[]
        {
            new MonsterGroup(Elites.Promote(Bestiary.Goblin), 1),
            new MonsterGroup(Bestiary.Goblin, 3),
        });
        var plain = new Encounter(new[] { new MonsterGroup(Bestiary.Goblin, 3) });

        Assert.True(withElite.HasElite);
        Assert.False(plain.HasElite);
    }

    [Fact]
    public void An_elite_encounter_drops_a_guaranteed_extra_prize()
    {
        var encounter = new Encounter(new[] { new MonsterGroup(Elites.Promote(Bestiary.Goblin), 1) });
        var drops = Loot.Roll(encounter, new SystemRandomSource(seed: 3), depth: 6);

        Assert.Contains(drops, i => i.IsMagic); // the guaranteed RollMagic from the elite block
    }

    [Fact]
    public void Wandering_packs_are_sometimes_led_by_an_elite()
    {
        var sawElite = false;
        for (var seed = 0; seed < 80 && !sawElite; seed++)
        {
            var encounter = new EncounterFactory(new SystemRandomSource(seed)).CreateRandom(depth: 10);
            if (encounter.HasElite) sawElite = true;
        }
        Assert.True(sawElite);
    }

    [Fact]
    public void Elite_chance_rises_with_depth_but_stays_bounded()
    {
        Assert.True(Elites.ChanceForDepth(12) > Elites.ChanceForDepth(1));
        Assert.True(Elites.ChanceForDepth(50) <= 0.25);
    }
}
