using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Items;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

/// <summary>Locks in the balance-pass intent: powerful accessories are earned, not bought.</summary>
public class BalanceTests
{
    private static System.Collections.Generic.List<Item> ShopAccessories() =>
        new GameSession(seed: 1).ShopStock.Where(i => i.IsAccessory).ToList();

    [Fact]
    public void Garth_only_stocks_entry_tier_defensive_accessories()
    {
        var shop = ShopAccessories();
        Assert.NotEmpty(shop);

        // No combat-effect or vitality accessory is for sale — those are treasure-only.
        Assert.All(shop, a => Assert.False(
            a.HitBonus > 0 || a.DamageBonus > 0 || a.RegenPerRound > 0 || a.LuckBonus > 0
            || a.MaxHitPointBonus > 0 || a.MaxSpellPointBonus > 0 || a.ImmuneStatus != StatusEffect.None,
            $"{a.Name} should not be sold at Garth's"));

        // The entry wards and protection ring stay available to buy.
        Assert.Contains(Items.RingOfProtection, shop);
        Assert.Contains(Items.RingOfFireWard, shop);
    }

    [Fact]
    public void The_potent_accessories_are_not_for_sale()
    {
        var shop = ShopAccessories();
        Assert.DoesNotContain(Items.AmuletOfVitality, shop);
        Assert.DoesNotContain(Items.RingOfFreeAction, shop);
        Assert.DoesNotContain(Items.AmuletOfTheMagi, shop);
        Assert.DoesNotContain(Items.RingOfStriking, shop);
    }

    [Fact]
    public void Elite_xp_and_gold_rewards_stay_in_step()
    {
        var basic = Bestiary.Orc;
        var elite = Elites.Promote(basic);
        Assert.Equal((int)System.Math.Round(basic.ExperienceValue * 2.5), elite.ExperienceValue);
        Assert.Equal((int)System.Math.Round(basic.GoldValue * 2.5), elite.GoldValue);
    }

    // --- Early-game survivability (level-1 round-1 wipe fix) ---

    // Starting heroes are never one unlucky roll from death: every member has at least the floor.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(99)]
    public void Default_party_members_start_with_a_survivable_hp_floor(int seed)
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed));
        Assert.All(party.Members, m => Assert.True(m.MaxHitPoints >= 6,
            $"{m.Name} ({m.Class}) started with only {m.MaxHitPoints} HP"));
    }

    // A d12 warrior gets a full hit die at level 1 (12 + base 2 + CON), so the front rank is sturdy.
    [Fact]
    public void A_warrior_gets_a_full_hit_die_at_first_level()
    {
        var hero = new CharacterFactory(new SystemRandomSource(seed: 3))
            .Create("Bryn", Race.Dwarf, CharacterClass.Warrior);
        Assert.True(hero.MaxHitPoints >= 12, $"warrior started with only {hero.MaxHitPoints} HP");
    }

    // The bug this guards against: the entry floors used to spawn MORE groups than deeper ones.
    // Depth 1 must yield exactly one ordinary group (an occasional elite is a separate, flagged group).
    [Fact]
    public void Depth_one_spawns_a_single_ordinary_group()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var enc = new EncounterFactory(new SystemRandomSource(seed)).CreateRandom(depth: 1);
            Assert.Equal(1, enc.Groups.Count(g => !g.Template.IsElite));
        }
    }

    // Deeper floors must be able to field more groups than the entry floor.
    [Fact]
    public void Group_count_scales_up_with_depth()
    {
        var shallowMax = 0;
        var deepMax = 0;
        for (var seed = 0; seed < 80; seed++)
        {
            shallowMax = System.Math.Max(shallowMax,
                new EncounterFactory(new SystemRandomSource(seed)).CreateRandom(1).Groups.Count(g => !g.Template.IsElite));
            deepMax = System.Math.Max(deepMax,
                new EncounterFactory(new SystemRandomSource(seed)).CreateRandom(16).Groups.Count(g => !g.Template.IsElite));
        }
        Assert.Equal(1, shallowMax);
        Assert.True(deepMax > shallowMax, $"deep floors ({deepMax}) should field more groups than the entry floor ({shallowMax})");
    }
}
