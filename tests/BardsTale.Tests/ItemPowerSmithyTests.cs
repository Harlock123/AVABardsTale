using System.Collections.Generic;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Items;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

public class ItemPowerSmithyTests
{
    // --- Item powers ---

    [Fact]
    public void A_powered_item_resolves_as_a_free_attack_spell()
    {
        var rng = new SystemRandomSource(seed: 5);
        var party = NewGame.CreateDefaultParty(rng);
        var hero = party.Members[0];
        hero.Weapon = Items.WandOfFlames;
        var startSp = hero.SpellPoints;

        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Troll, 1) });
        var engine = new CombatEngine(party, encounter, rng, surprise: SurpriseState.None);
        var troll = encounter.Groups[0].Monsters[0];
        var hpBefore = troll.HitPoints;

        engine.ExecuteRound(new List<CombatCommand>
        {
            new(hero, CombatActionType.CastSpell, 0, Spell: Items.WandOfFlames.ItemPower)
        });

        Assert.True(troll.HitPoints < hpBefore, "the wand should damage the troll");
        Assert.Equal(startSp, hero.SpellPoints); // free — no spell points spent
    }

    [Fact]
    public void Powered_items_are_magic_and_resolve_by_name_for_saves()
    {
        Assert.True(Items.WandOfFlames.HasPower);
        Assert.True(Items.WandOfFlames.IsMagic);
        Assert.NotNull(Items.WandOfFlames.ItemPower);
        Assert.Equal(Items.WandOfFlames, Items.Find("Wand of Flames"));
        Assert.Equal(Items.ForgeEmber, Items.Find("Forge Ember"));
        Assert.Equal(ItemSlot.Material, Items.ForgeEmber.Slot);
    }

    // --- Smithy upgrades ---

    [Fact]
    public void Upgrading_raises_the_enchantment_and_caps_at_plus_three()
    {
        var plus1 = Items.UpgradeOf(Items.LongSword);
        Assert.NotNull(plus1);
        Assert.Equal(1, plus1!.MagicBonus);
        Assert.Equal("Long Sword +1", plus1.Name);

        var plus2 = Items.UpgradeOf(plus1);
        var plus3 = Items.UpgradeOf(plus2!);
        Assert.Equal(2, plus2!.MagicBonus);
        Assert.Equal(3, plus3!.MagicBonus);
        Assert.Null(Items.UpgradeOf(plus3)); // capped at +3

        Assert.Equal(plus3, Items.Find("Long Sword +3")); // upgraded items resolve on load
    }

    [Fact]
    public void Upgrade_cost_scales_with_the_target_bonus()
    {
        var (gold1, embers1) = Items.UpgradeCost(Items.LongSword);             // → +1
        var (gold3, embers3) = Items.UpgradeCost(Items.Enchant(Items.LongSword, 2)); // → +3

        Assert.True(gold3 > gold1);
        Assert.Equal(1, embers1);
        Assert.Equal(3, embers3);
    }

    [Fact]
    public void Non_enchantable_items_cannot_be_forged()
    {
        Assert.Null(Items.UpgradeOf(Items.Fists));
        Assert.Null(Items.UpgradeOf(Items.HealingPotion));
        Assert.Null(Items.UpgradeOf(Items.WandOfFlames)); // powered items aren't enchant bases
    }

    // --- Loot ---

    [Fact]
    public void Bosses_on_the_deeper_floors_yield_forge_embers()
    {
        var drops = Loot.Roll(Bosses.Create(8), new SystemRandomSource(seed: 1), depth: 8);
        Assert.Contains(Items.ForgeEmber, drops);
    }
}
