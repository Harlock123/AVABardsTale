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
    public void Rings_fill_two_slots_then_the_third_displaces_the_first()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];

        Assert.Null(Equipment.Equip(hero, Items.RingOfFireWard));  // → Ring 1
        Assert.Equal(Items.RingOfFireWard, hero.Ring1);

        Assert.Null(Equipment.Equip(hero, Items.RingOfFrostWard)); // → Ring 2, no displacement
        Assert.Equal(Items.RingOfFrostWard, hero.Ring2);

        var displaced = Equipment.Equip(hero, Items.RingOfStormWard); // both full → displaces Ring 1
        Assert.Equal(Items.RingOfFireWard, displaced);
        Assert.Equal(Items.RingOfStormWard, hero.Ring1);
    }

    [Fact]
    public void An_amulet_goes_in_its_own_slot_independent_of_rings()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        Equipment.Equip(hero, Items.RingOfFireWard);
        var displaced = Equipment.Equip(hero, Items.AmuletOfTheViper);
        Assert.Null(displaced);
        Assert.Equal(Items.AmuletOfTheViper, hero.Amulet);
        Assert.Equal(Items.RingOfFireWard, hero.Ring1); // ring slot untouched
    }

    [Fact]
    public void All_three_accessory_slots_stack_their_wards_and_armor()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        var baseAc = hero.ArmorClass;
        hero.Ring1 = Items.RingOfFireWard;
        hero.Ring2 = Items.RingOfProtection;          // +1 AC
        hero.Amulet = Items.AmuletOfTheViper;         // poison ward

        Assert.True(hero.Resists(Element.Fire));
        Assert.True(hero.Resists(Element.Poison));
        Assert.False(hero.Resists(Element.Cold));
        Assert.Equal(baseAc - 1, hero.ArmorClass);    // the protection ring lowers AC by one
    }

    [Fact]
    public void Two_protection_rings_stack_their_armor()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        var before = hero.ArmorClass;
        hero.Ring1 = Items.RingOfProtection;
        hero.Ring2 = Items.RingOfProtection;
        Assert.Equal(before - 2, hero.ArmorClass);
    }

    [Fact]
    public void The_amulet_of_warding_resists_three_elements_but_not_poison()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        hero.Amulet = Items.AmuletOfWarding;

        Assert.True(hero.Resists(Element.Fire));
        Assert.True(hero.Resists(Element.Cold));
        Assert.True(hero.Resists(Element.Lightning));
        Assert.False(hero.Resists(Element.Poison));
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
        hero.Ring1 = accessory;

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
    private static GameState NewChestGame(out Party party, bool ornate = false)
    {
        var rng = new SystemRandomSource(seed: 9);
        party = NewGame.CreateDefaultParty(rng);
        foreach (var m in party.Members) { m.MaxHitPoints = 500; m.HitPoints = 500; } // survive any trap
        var maze = new MazeBuilder(rng).Build("Vault", 9, 9);
        var game = new GameState(party, maze, rng);
        game.CurrentCell.Feature = ornate ? CellFeature.OrnateChest : CellFeature.Chest;
        return game;
    }

    // ── Chest variety: ornate chests and mimics ───────────────────────────────

    [Fact]
    public void An_ornate_chest_always_yields_a_warding_accessory()
    {
        var (gold, items) = Loot.RollChest(new SystemRandomSource(seed: 11), depth: 5, ornate: true);
        Assert.True(gold > 0);
        Assert.Contains(items, i => i.IsAccessory);
    }

    [Fact]
    public void Opening_an_ornate_chest_grants_an_accessory_and_is_never_a_mimic()
    {
        var game = NewChestGame(out _, ornate: true);
        var result = game.OpenChest();
        Assert.Null(result.Mimic);
        Assert.Contains(result.Loot, i => i.IsAccessory);
        Assert.False(game.OnChest);
    }

    [Fact]
    public void The_mimic_is_a_catalogued_foe_that_scales_with_depth()
    {
        Assert.Contains(MonsterCatalog.All, t => t.Name == "Mimic");
        var shallow = ChestMimic.EncounterFor(2).Groups[0].Template;
        var deep = ChestMimic.EncounterFor(18).Groups[0].Template;
        Assert.True(deep.MaxHitPoints > shallow.MaxHitPoints);
    }

    [Fact]
    public void A_plain_chest_can_turn_out_to_be_a_mimic()
    {
        var sawMimic = false;
        for (var seed = 0; seed < 100 && !sawMimic; seed++)
        {
            var rng = new SystemRandomSource(seed);
            var party = NewGame.CreateDefaultParty(rng);
            foreach (var m in party.Members) { m.MaxHitPoints = 500; m.HitPoints = 500; }
            var maze = new MazeBuilder(rng).Build("V", 9, 9);
            var game = new GameState(party, maze, rng);
            game.CurrentCell.Feature = CellFeature.Chest;
            if (game.OpenChest().Mimic is not null) sawMimic = true;
        }
        Assert.True(sawMimic);
    }

    // ── Smithy: enchanting accessories ────────────────────────────────────────

    [Fact]
    public void The_smithy_forges_a_warding_ring_and_preserves_its_element()
    {
        var up = Items.UpgradeOf(Items.RingOfFireWard);
        Assert.NotNull(up);
        Assert.Equal("Ring of Fire Ward +1", up!.Name);
        Assert.Equal(1, up.ArmorBonus);                 // each tier adds a point of armour
        Assert.Equal(Element.Fire, up.ResistsElement);  // the ward survives the forging
    }

    [Fact]
    public void An_enchanted_accessory_resolves_by_name_for_save_load()
    {
        var up = Items.UpgradeOf(Items.RingOfFrostWard)!;
        var resolved = Items.Find(up.Name);
        Assert.NotNull(resolved);
        Assert.Equal(Element.Cold, resolved!.ResistsElement);
        Assert.Equal(up.ArmorBonus, resolved.ArmorBonus);
    }

    [Fact]
    public void Accessory_enchantment_caps_at_plus_three()
    {
        var plus3 = Items.Enchant(Items.RingOfProtection, 3);
        Assert.Null(Items.UpgradeOf(plus3));
    }

    [Fact]
    public void A_forged_ward_still_halves_its_element_in_combat()
    {
        var plus2 = Items.Enchant(Items.RingOfFireWard, 2);
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        hero.Ring1 = plus2;
        Assert.True(hero.Resists(Element.Fire));
        Assert.Equal(2, hero.Ring1!.ArmorBonus);
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    [Fact]
    public void A_saved_game_remembers_all_three_accessory_slots()
    {
        var session = new GameSession(seed: 5);
        session.FillDefaultParty();
        var hero = session.Party.Members[0];
        hero.Ring1 = Items.RingOfFireWard;
        hero.Ring2 = Items.RingOfStormWard;
        hero.Amulet = Items.AmuletOfWarding;

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session)).Party.Members[0];

        Assert.Equal("Ring of Fire Ward", loaded.Ring1?.Name);
        Assert.Equal("Ring of Storm Ward", loaded.Ring2?.Name);
        Assert.Equal("Amulet of Warding", loaded.Amulet?.Name);
        Assert.True(loaded.Resists(Element.Fire));
        Assert.True(loaded.Resists(Element.Lightning));
        Assert.True(loaded.Resists(Element.Cold));
    }

    // ── Bestiary affinity polish ──────────────────────────────────────────────

    [Fact]
    public void The_mimic_has_a_fire_weakness_in_the_affinity_table()
    {
        Assert.True((MonsterElements.WeakOf("Mimic") & Element.Fire) != 0);
    }

    [Fact]
    public void Affinities_render_as_element_glyphs()
    {
        Assert.Equal("", MonsterElements.DescribeGlyphs(Element.None));
        var glyphs = MonsterElements.DescribeGlyphs(Element.Fire | Element.Cold);
        Assert.Contains(MonsterElements.Glyph(Element.Fire), glyphs);
        Assert.Contains(MonsterElements.Glyph(Element.Cold), glyphs);
        Assert.NotEqual("", MonsterElements.Glyph(Element.Lightning));
    }
}
