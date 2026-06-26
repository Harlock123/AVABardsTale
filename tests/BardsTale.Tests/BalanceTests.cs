using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Items;
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
}
