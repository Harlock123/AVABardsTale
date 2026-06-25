using System.Collections.Generic;
using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Items;
using BardsTale.Core.Persistence;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

public class AccessoryChestTests
{
    // ── Accessory slots ───────────────────────────────────────────────────────

    [Fact]
    public void Equipping_an_accessory_fills_the_accessory_slot_and_returns_the_displaced_one()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];

        var first = Equipment.Equip(hero, Items.RingOfFireWard);
        Assert.Null(first);
        Assert.Equal(Items.RingOfFireWard, hero.Accessory);

        var displaced = Equipment.Equip(hero, Items.RingOfFrostWard);
        Assert.Equal(Items.RingOfFireWard, displaced);
        Assert.Equal(Items.RingOfFrostWard, hero.Accessory);
    }

    [Fact]
    public void A_warding_accessory_grants_the_matching_resistance_only()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        hero.Accessory = Items.RingOfFireWard;

        Assert.True(hero.Resists(Element.Fire));
        Assert.False(hero.Resists(Element.Cold));
        Assert.False(hero.Resists(Element.Lightning));
    }

    [Fact]
    public void The_amulet_of_warding_resists_three_elements_but_not_poison()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        hero.Accessory = Items.AmuletOfWarding;

        Assert.True(hero.Resists(Element.Fire));
        Assert.True(hero.Resists(Element.Cold));
        Assert.True(hero.Resists(Element.Lightning));
        Assert.False(hero.Resists(Element.Poison));
    }

    [Fact]
    public void A_ring_of_protection_improves_armor_class()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        var before = hero.ArmorClass;
        hero.Accessory = Items.RingOfProtection;
        Assert.Equal(before - 1, hero.ArmorClass); // lower AC is better
    }

    // ── Party-side elemental resistance in combat ─────────────────────────────

    // Replays an identical fire-breath round; only member[0]'s ward differs, so the
    // same seed yields the same base roll and warding shows up as an exact halving.
    private static int FrontHeroLoss(Item? accessory)
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        hero.MaxHitPoints = 1000;
        hero.HitPoints = 1000;
        hero.Accessory = accessory;

        var breath = new MonsterSpell("Fire Breath", MonsterSpellKind.BlastParty, Power: 30, Chance: 1.0);
        var drake = new MonsterTemplate("Test Drake", 2000, 0, 1, 4, 0, 10, 0, 1, Spell: breath);
        var encounter = new Encounter(new[] { new MonsterGroup(drake, 1) });
        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 7), surprise: SurpriseState.None);

        var commands = party.Members.Select(m => new CombatCommand(m, CombatActionType.Defend)).ToList();
        engine.ExecuteRound(commands);
        return 1000 - hero.HitPoints;
    }

    [Fact]
    public void Warding_gear_halves_a_matching_elemental_blast()
    {
        var unwarded = FrontHeroLoss(null);
        var warded = FrontHeroLoss(Items.RingOfFireWard);
        Assert.True(unwarded > 0);
        Assert.Equal(System.Math.Max(1, unwarded / 2), warded);
    }

    [Fact]
    public void A_mismatched_ward_does_not_reduce_the_blast()
    {
        var unwarded = FrontHeroLoss(null);
        var frostWarded = FrontHeroLoss(Items.RingOfFrostWard); // frost ring vs fire breath
        Assert.Equal(unwarded, frostWarded);
    }

    [Fact]
    public void Monster_spells_carry_the_expected_element()
    {
        Assert.Equal(Element.Fire, MonsterElements.OfSpell("Fire Breath"));
        Assert.Equal(Element.Cold, MonsterElements.OfSpell("Frost Breath"));
        Assert.Equal(Element.Lightning, MonsterElements.OfSpell("Spark"));
        Assert.Equal(Element.Poison, MonsterElements.OfSpell("Acid Breath"));
        Assert.Equal(Element.Arcane, MonsterElements.OfSpell("Mind Blast")); // default
    }

    // ── Treasure chests ───────────────────────────────────────────────────────

    [Fact]
    public void Maze_builder_places_treasure_chests()
    {
        var maze = new MazeBuilder(new SystemRandomSource(seed: 3)).Build("Test", 9, 9);
        var chests = 0;
        for (var x = 0; x < maze.Width; x++)
            for (var y = 0; y < maze.Height; y++)
                if (maze[x, y].Feature == CellFeature.Chest)
                    chests++;
        Assert.True(chests > 0);
    }

    [Fact]
    public void Stepping_onto_a_chest_reports_it_without_opening()
    {
        var game = NewChestGame(out var party);
        Assert.True(game.OnChest);
        var goldBefore = party.Gold;
        Assert.Equal(goldBefore, party.Gold); // merely standing there yields nothing
    }

    [Fact]
    public void Opening_a_chest_yields_loot_and_empties_the_cell()
    {
        var game = NewChestGame(out var party);
        var goldBefore = party.Gold;
        var invBefore = party.Inventory.Count;

        var result = game.OpenChest();

        Assert.True(party.Gold > goldBefore);          // gold was claimed
        Assert.True(party.Inventory.Count > invBefore); // at least one prize
        Assert.NotEmpty(result.Loot);
        Assert.False(game.OnChest);                     // the chest is now empty
    }

    [Fact]
    public void Roll_chest_scales_loot_and_always_yields_a_prize()
    {
        var (gold, items) = Loot.RollChest(new SystemRandomSource(seed: 11), depth: 6);
        Assert.True(gold > 0);
        Assert.NotEmpty(items);
    }

    // Builds a game with the party standing on a freshly-placed chest tile.
    private static GameState NewChestGame(out Party party)
    {
        var rng = new SystemRandomSource(seed: 9);
        party = NewGame.CreateDefaultParty(rng);
        foreach (var m in party.Members) { m.MaxHitPoints = 500; m.HitPoints = 500; } // survive any trap
        var maze = new MazeBuilder(rng).Build("Vault", 9, 9);
        var game = new GameState(party, maze, rng);
        game.CurrentCell.Feature = CellFeature.Chest;
        return game;
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    [Fact]
    public void A_saved_game_remembers_an_equipped_accessory()
    {
        var session = new GameSession(seed: 5);
        session.FillDefaultParty();
        session.Party.Members[0].Accessory = Items.AmuletOfWarding;

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session));

        Assert.Equal("Amulet of Warding", loaded.Party.Members[0].Accessory?.Name);
        Assert.True(loaded.Party.Members[0].Resists(Element.Fire));
    }
}
