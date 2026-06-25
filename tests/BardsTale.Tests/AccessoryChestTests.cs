using System.Collections.Generic;
using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Items;
using BardsTale.Core.Persistence;
using BardsTale.Core.Town;
using BardsTale.Core.Util;
using BardsTale.UI.ViewModels;
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
    public void Two_protection_rings_stack_their_armor_plus_the_set_bonus()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        var before = hero.ArmorClass;
        hero.Ring1 = Items.RingOfProtection;
        hero.Ring2 = Items.RingOfProtection;
        // −1 each, plus −1 from the Twin Bulwark set the pair completes.
        Assert.Equal(before - 3, hero.ArmorClass);
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

    // ── New accessory effects ─────────────────────────────────────────────────

    [Fact]
    public void Combat_accessories_grant_hit_damage_luck_and_regen()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        var baseLuck = hero.Attributes.Luck;

        // Each effect checked in isolation, so no set bonus muddies the numbers.
        hero.Ring1 = Items.RingOfStriking;     // +2 damage
        Assert.Equal(2, hero.GearDamageBonus);

        hero.Ring1 = Items.RingOfAccuracy;     // +2 to-hit
        Assert.Equal(2, hero.GearHitBonus);

        hero.Ring1 = null;
        hero.Amulet = Items.AmuletOfFortune;   // +4 luck
        Assert.Equal(baseLuck + 4, hero.EffectiveLuck);

        hero.Amulet = null;
        hero.Ring1 = Items.RingOfRegeneration; // +2 regen/round
        Assert.Equal(2, hero.RegenPerRound);
    }

    [Fact]
    public void A_ring_of_free_action_grants_immunity_that_inflict_respects()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        hero.Ring1 = Items.RingOfFreeAction;

        Assert.True(hero.IsImmuneTo(StatusEffect.Paralyzed));
        Assert.True(hero.IsImmuneTo(StatusEffect.Asleep));
        Assert.False(hero.IsImmuneTo(StatusEffect.Poisoned));

        hero.Inflict(StatusEffect.Paralyzed);
        Assert.False(hero.IsParalyzed);   // warded off entirely
        hero.Inflict(StatusEffect.Poisoned);
        Assert.True(hero.IsPoisoned);      // not immune to poison
    }

    [Fact]
    public void A_regeneration_ring_mends_the_wearer_each_combat_round()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        hero.Ring1 = Items.RingOfRegeneration;
        hero.MaxHitPoints = 100;
        hero.HitPoints = 50;

        // A foe too feeble to kill anyone, so the round ends with everyone alive and regenerating.
        var t = new MonsterTemplate("Gnat", 2000, 0, 1, 1, 0, 1, 0, 1);
        var encounter = new Encounter(new[] { new MonsterGroup(t, 1) });
        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 3), surprise: SurpriseState.None);
        var commands = party.Members.Select(m => new CombatCommand(m, CombatActionType.Defend)).ToList();

        engine.ExecuteRound(commands);
        Assert.True(hero.HitPoints > 50); // regenerated at end of round
    }

    // ── Accessory set bonuses ─────────────────────────────────────────────────

    [Fact]
    public void Two_rings_of_protection_trigger_the_twin_bulwark_set()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        var bare = hero.ArmorClass;

        hero.Ring1 = Items.RingOfProtection;
        var oneRing = hero.ArmorClass;           // −1 AC from the ring alone
        hero.Ring2 = Items.RingOfProtection;     // second ring + the set bonus

        Assert.Equal(bare - 1, oneRing);
        Assert.Equal(bare - 3, hero.ArmorClass); // −1, −1, and −1 set bonus
        Assert.Contains(hero.ActiveSets, s => s.Name == "Twin Bulwark");
    }

    [Fact]
    public void A_ring_and_amulet_set_adds_an_extra_ward()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        hero.Amulet = Items.AmuletOfWarding;  // fire/cold/lightning, but not arcane
        Assert.False(hero.Resists(Element.Arcane));   // no arcane ward on its own

        // Adding the Storm Ward ring completes the Stormwarden set, which grants an arcane ward.
        hero.Ring1 = Items.RingOfStormWard;
        Assert.Contains(hero.ActiveSets, s => s.Name == "Stormwarden");
        Assert.True(hero.Resists(Element.Arcane));
    }

    [Fact]
    public void Set_pieces_still_match_after_being_enchanted()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        hero.Ring1 = Items.Enchant(Items.RingOfProtection, 2); // "Ring of Protection +2"
        hero.Ring2 = Items.RingOfProtection;
        Assert.Contains(hero.ActiveSets, s => s.Name == "Twin Bulwark");
    }

    // ── Per-slot equip control (Garth's) ──────────────────────────────────────

    [Fact]
    public void Equipping_a_ring_to_a_chosen_slot_displaces_only_that_slot()
    {
        var session = new GameSession(seed: 2);
        session.FillDefaultParty();
        session.Party.Inventory.Add(Items.RingOfFireWard);
        session.Party.Inventory.Add(Items.RingOfFrostWard);
        session.TownPosition = session.Town.Buildings.First(b => b.Building == TownBuilding.Shop).Position;
        var town = new TownViewModel(session);
        town.EnterCommand.Execute(null);

        var hero = town.Party.First();
        hero.Model.Ring1 = Items.RingOfStormWard; // pre-fill Ring 1
        town.SelectedHero = hero;
        town.SelectedStashItem = town.Stash.First(s => s.Item == Items.RingOfFireWard);

        town.EquipAccessoryCommand.Execute("Ring 2");

        Assert.Equal(Items.RingOfStormWard, hero.Model.Ring1);  // untouched
        Assert.Equal(Items.RingOfFireWard, hero.Model.Ring2);   // newly equipped
        Assert.DoesNotContain(Items.RingOfFireWard, session.Party.Inventory);
    }

    [Fact]
    public void Equipping_over_a_full_slot_returns_the_old_ring_to_the_stash()
    {
        var session = new GameSession(seed: 2);
        session.FillDefaultParty();
        session.Party.Inventory.Add(Items.RingOfFireWard);
        session.TownPosition = session.Town.Buildings.First(b => b.Building == TownBuilding.Shop).Position;
        var town = new TownViewModel(session);
        town.EnterCommand.Execute(null);

        var hero = town.Party.First();
        hero.Model.Ring1 = Items.RingOfStormWard;
        town.SelectedHero = hero;
        town.SelectedStashItem = town.Stash.First(s => s.Item == Items.RingOfFireWard);

        town.EquipAccessoryCommand.Execute("Ring 1");

        Assert.Equal(Items.RingOfFireWard, hero.Model.Ring1);
        Assert.Contains(Items.RingOfStormWard, session.Party.Inventory); // displaced ring returns
    }

    [Fact]
    public void An_amulet_cannot_be_equipped_into_a_ring_slot()
    {
        var session = new GameSession(seed: 2);
        session.FillDefaultParty();
        session.Party.Inventory.Add(Items.AmuletOfWarding);
        session.TownPosition = session.Town.Buildings.First(b => b.Building == TownBuilding.Shop).Position;
        var town = new TownViewModel(session);
        town.EnterCommand.Execute(null);

        var hero = town.Party.First();
        town.SelectedHero = hero;
        town.SelectedStashItem = town.Stash.First(s => s.Item == Items.AmuletOfWarding);

        town.EquipAccessoryCommand.Execute("Ring 1");

        Assert.Null(hero.Model.Ring1);
        Assert.Contains(Items.AmuletOfWarding, session.Party.Inventory); // rejected, stays in stash
    }

    [Fact]
    public void Auto_equip_keeps_the_stronger_ring_when_both_slots_are_full()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        hero.Ring1 = Items.RingOfProtection;                  // value 500 (weaker)
        hero.Ring2 = Items.Enchant(Items.RingOfFireWard, 3);  // pricey (stronger)

        var displaced = Equipment.Equip(hero, Items.RingOfStormWard);

        Assert.Equal(Items.RingOfProtection, displaced);      // the weaker ring is bumped
        Assert.Equal(Items.RingOfStormWard, hero.Ring1);      // new ring takes its slot
        Assert.Equal("Ring of Fire Ward +3", hero.Ring2!.Name); // the stronger ring stays on
    }

    [Fact]
    public void Auto_equip_fills_an_empty_ring_slot_first()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        hero.Ring1 = Items.RingOfProtection;

        var displaced = Equipment.Equip(hero, Items.RingOfFireWard);

        Assert.Null(displaced);                          // nothing displaced
        Assert.Equal(Items.RingOfFireWard, hero.Ring2);  // filled the free slot
    }

    // ── Set discovery hints ───────────────────────────────────────────────────

    [Fact]
    public void Wearing_one_set_piece_hints_at_the_missing_one()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        hero.Amulet = Items.AmuletOfWarding; // one half of Stormwarden

        Assert.Contains(hero.SetHints, h => h.Contains("Ring of Storm Ward") && h.Contains("Stormwarden"));
    }

    [Fact]
    public void A_completed_set_offers_no_hint()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        hero.Ring1 = Items.RingOfStormWard;
        hero.Amulet = Items.AmuletOfWarding;

        Assert.Contains(hero.ActiveSets, s => s.Name == "Stormwarden");
        Assert.DoesNotContain(hero.SetHints, h => h.Contains("Stormwarden"));
    }

    [Fact]
    public void No_set_pieces_means_no_hints()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        Assert.Empty(party.Members[0].SetHints);
    }

    [Fact]
    public void A_second_protection_ring_is_hinted_as_another()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        hero.Ring1 = Items.RingOfProtection;
        Assert.Contains(hero.SetHints, h => h.Contains("another Ring of Protection") && h.Contains("Twin Bulwark"));
    }

    // ── Combat feedback for effects ───────────────────────────────────────────

    [Fact]
    public void Regeneration_is_announced_in_the_combat_log()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        hero.Ring1 = Items.RingOfRegeneration;
        hero.MaxHitPoints = 100;
        hero.HitPoints = 50;

        var t = new MonsterTemplate("Gnat", 2000, 0, 1, 1, 0, 1, 0, 1);
        var encounter = new Encounter(new[] { new MonsterGroup(t, 1) });
        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 3), surprise: SurpriseState.None);
        var commands = party.Members.Select(m => new CombatCommand(m, CombatActionType.Defend)).ToList();

        var round = engine.ExecuteRound(commands);
        Assert.Contains(round.Log, l => l.Contains($"{hero.Name} regenerates"));
    }

    [Fact]
    public void A_warded_blast_is_noted_in_the_combat_log()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        hero.MaxHitPoints = 500;
        hero.HitPoints = 500;
        hero.Ring1 = Items.RingOfFireWard;

        var breath = new MonsterSpell("Fire Breath", MonsterSpellKind.BlastParty, Power: 20, Chance: 1.0);
        var drake = new MonsterTemplate("Test Drake", 2000, 0, 1, 1, 0, 10, 0, 1, Spell: breath);
        var encounter = new Encounter(new[] { new MonsterGroup(drake, 1) });
        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 7), surprise: SurpriseState.None);
        var commands = party.Members.Select(m => new CombatCommand(m, CombatActionType.Defend)).ToList();

        var round = engine.ExecuteRound(commands);
        Assert.Contains(round.Log, l => l.Contains("warded against fire"));
    }

    // ── More sets, a three-piece set, and themed drops ────────────────────────

    [Fact]
    public void Berserkers_fury_stacks_hit_and_damage_from_two_rings()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        hero.Ring1 = Items.RingOfStriking;   // +2 dmg
        hero.Ring2 = Items.RingOfAccuracy;   // +2 hit

        Assert.Contains(hero.ActiveSets, s => s.Name == "Berserker's Fury");
        Assert.Equal(3, hero.GearDamageBonus); // 2 + set's +1
        Assert.Equal(3, hero.GearHitBonus);    // 2 + set's +1
    }

    [Fact]
    public void Wardens_resolve_grants_full_status_immunity()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        hero.Ring1 = Items.RingOfFreeAction;  // paralysis + sleep
        hero.Amulet = Items.AmuletOfTheViper; // poison ward; set adds poison immunity

        Assert.Contains(hero.ActiveSets, s => s.Name == "Warden's Resolve");
        Assert.True(hero.IsImmuneTo(StatusEffect.Paralyzed));
        Assert.True(hero.IsImmuneTo(StatusEffect.Asleep));
        Assert.True(hero.IsImmuneTo(StatusEffect.Poisoned));
    }

    [Fact]
    public void The_three_piece_regalia_wards_every_element()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        var bareAc = hero.ArmorClass;
        hero.Ring1 = Items.RingOfFireWard;    // fire
        hero.Ring2 = Items.RingOfFrostWard;   // cold
        hero.Amulet = Items.AmuletOfTheViper; // poison; set completes lightning + arcane

        Assert.Contains(hero.ActiveSets, s => s.Name == "Elementalist's Regalia");
        foreach (var element in new[] { Element.Fire, Element.Cold, Element.Lightning, Element.Poison, Element.Arcane })
            Assert.True(hero.Resists(element), $"should ward {element}");
        Assert.Equal(bareAc - 1, hero.ArmorClass); // the regalia's +1 armour
    }

    [Fact]
    public void Ornate_chests_sometimes_drop_a_matched_set()
    {
        var found = false;
        for (var seed = 0; seed < 80 && !found; seed++)
        {
            var (_, items) = Loot.RollChest(new SystemRandomSource(seed), depth: 6, ornate: true);
            var worn = items.Where(i => i.IsAccessory).Select(i => Items.BaseName(i.Name)).ToList();
            if (AccessorySets.All.Any(set => Covers(worn, set.Pieces)))
                found = true;
        }
        Assert.True(found, "expected at least one ornate chest to drop a complete matched set");
    }

    // Multiset cover: every required piece matched by a distinct item in the haul.
    private static bool Covers(List<string> worn, System.Collections.Generic.IReadOnlyList<string> required)
    {
        var pool = new List<string>(worn);
        foreach (var piece in required)
            if (!pool.Remove(piece)) return false;
        return true;
    }

    // ── Camping in the dungeon ────────────────────────────────────────────────

    private static GameState NewDungeon(int seed, out Party party)
    {
        var rng = new SystemRandomSource(seed);
        party = NewGame.CreateDefaultParty(rng);
        var maze = new MazeBuilder(rng).Build("Camp", 9, 9);
        return new GameState(party, maze, rng);
    }

    [Fact]
    public void Resting_at_camp_recovers_hit_and_spell_points()
    {
        var rested = false;
        for (var seed = 0; seed < 80 && !rested; seed++)
        {
            var game = NewDungeon(seed, out var party);
            foreach (var m in party.Members) { m.MaxHitPoints = 100; m.HitPoints = 10; }
            var result = game.Camp();
            if (result.Rested)
            {
                rested = true;
                Assert.Null(result.Ambush);
                Assert.True(party.Members[0].HitPoints > 10);
            }
        }
        Assert.True(rested, "expected at least one undisturbed rest");
    }

    // ── Boss set rewards & set callouts ───────────────────────────────────────

    [Fact]
    public void A_set_guardian_boss_drops_the_whole_set()
    {
        // Stone Titan lairs on floor 11 and guards the Twin Bulwark set (two protection rings).
        var encounter = Bosses.Create(11);
        Assert.Equal("Stone Titan", encounter.Groups[0].Template.Name);

        var drops = Loot.Roll(encounter, new SystemRandomSource(seed: 4), depth: 11);
        Assert.Equal(2, drops.Count(i => i.Name == "Ring of Protection"));
    }

    [Fact]
    public void The_deep_dragon_boss_drops_the_three_piece_regalia()
    {
        var encounter = Bosses.Create(19); // Dragon Tyrant → Elementalist's Regalia
        var drops = Loot.Roll(encounter, new SystemRandomSource(seed: 4), depth: 19);

        Assert.Contains(drops, i => i.Name == "Ring of Fire Ward");
        Assert.Contains(drops, i => i.Name == "Ring of Frost Ward");
        Assert.Contains(drops, i => i.Name == "Amulet of the Viper");
    }

    [Fact]
    public void Sets_in_detects_a_complete_set_in_a_haul()
    {
        var haul = new List<Item> { Items.RingOfProtection, Items.RingOfProtection, Items.LongSword };
        Assert.Contains(AccessorySets.SetsIn(haul), s => s.Name == "Twin Bulwark");

        var partial = new List<Item> { Items.RingOfProtection, Items.LongSword };
        Assert.DoesNotContain(AccessorySets.SetsIn(partial), s => s.Name == "Twin Bulwark");
    }

    [Fact]
    public void A_boss_kill_announces_the_recovered_set_by_name()
    {
        var game = NewDungeon(7, out _);
        var log = game.ApplyVictory(Bosses.Create(11)); // Stone Titan → Twin Bulwark
        Assert.Contains(log, l => l.Contains("Twin Bulwark"));
    }

    // ── Camp risk mitigation ──────────────────────────────────────────────────

    [Fact]
    public void A_watchful_rogue_and_bard_lower_the_camp_ambush_risk()
    {
        var game = NewDungeon(1, out var party);
        var withHelpers = game.CampAmbushChance;

        // Down the watchers and the camp grows more dangerous.
        foreach (var m in party.Members.Where(m => m.Class == BardsTale.Core.Characters.CharacterClass.Rogue || m.CanSing))
            m.ApplyDamage(9999);

        Assert.True(game.CampAmbushChance > withHelpers);
    }

    [Fact]
    public void A_camp_can_be_ambushed_by_wandering_monsters()
    {
        var ambushed = false;
        for (var seed = 0; seed < 80 && !ambushed; seed++)
        {
            var game = NewDungeon(seed, out _);
            var result = game.Camp();
            if (!result.Rested)
            {
                ambushed = true;
                Assert.NotNull(result.Ambush);
            }
        }
        Assert.True(ambushed, "expected at least one ambushed camp");
    }

    [Fact]
    public void Unequipping_a_slot_returns_the_accessory_to_the_stash()
    {
        var session = new GameSession(seed: 2);
        session.FillDefaultParty();
        session.TownPosition = session.Town.Buildings.First(b => b.Building == TownBuilding.Shop).Position;
        var town = new TownViewModel(session);
        town.EnterCommand.Execute(null);

        var hero = town.Party.First();
        hero.Model.Amulet = Items.AmuletOfFortune;
        town.SelectedHero = hero;

        town.UnequipAccessoryCommand.Execute("Amulet");

        Assert.Null(hero.Model.Amulet);
        Assert.Contains(Items.AmuletOfFortune, session.Party.Inventory);
    }
}
