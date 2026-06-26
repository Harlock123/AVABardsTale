using System.Linq;
using BardsTale.Core.Game;
using BardsTale.Core.Items;
using Xunit;

namespace BardsTale.Tests;

/// <summary>Covers Garth's progress-gated restocking — stronger accessories unlock with depth.</summary>
public class ShopRestockTests
{
    [Fact]
    public void At_the_start_only_entry_wares_are_stocked()
    {
        var stock = ShopWares.For(1);
        Assert.Contains(Items.RingOfFireWard, stock);   // entry ward
        Assert.DoesNotContain(Items.RingOfStriking, stock);
        Assert.DoesNotContain(Items.AmuletOfVitality, stock);
    }

    [Fact]
    public void Reaching_floor_four_unlocks_the_first_effect_accessories()
    {
        var stock = ShopWares.For(4);
        Assert.Contains(Items.RingOfStriking, stock);
        Assert.Contains(Items.AmuletOfFortune, stock);
        Assert.DoesNotContain(Items.RingOfRegeneration, stock); // not until floor 8
    }

    [Fact]
    public void The_deepest_wares_unlock_late()
    {
        Assert.Contains(Items.RingOfRegeneration, ShopWares.For(8));
        Assert.Contains(Items.AmuletOfWarding, ShopWares.For(12));
        Assert.Contains(Items.AmuletOfVitality, ShopWares.For(16));
    }

    [Fact]
    public void The_legendary_talisman_stays_treasure_only()
        => Assert.DoesNotContain(Items.TalismanOfTheAges, ShopWares.For(20));

    [Fact]
    public void Stock_only_grows_as_the_party_delves_deeper()
    {
        var shallow = ShopWares.For(1).Count;
        var mid = ShopWares.For(8).Count;
        var deep = ShopWares.For(16).Count;
        Assert.True(mid > shallow);
        Assert.True(deep > mid);
    }

    [Fact]
    public void The_next_unlock_floor_is_reported_then_runs_out()
    {
        Assert.Equal(4, ShopWares.NextUnlockDepth(1));
        Assert.Equal(8, ShopWares.NextUnlockDepth(5));
        Assert.Null(ShopWares.NextUnlockDepth(20));
    }

    [Fact]
    public void Crossing_an_unlock_floor_is_reported_for_the_restock_notice()
    {
        // A dive from floor 3 to floor 9 crosses the floor-4 and floor-8 unlocks.
        var crossed = ShopWares.NewUnlocksBetween(previousDeepest: 3, currentDeepest: 9);
        Assert.Equal(new[] { 4, 8 }, crossed);
    }

    [Fact]
    public void Diving_no_deeper_than_before_unlocks_nothing()
    {
        Assert.Empty(ShopWares.NewUnlocksBetween(previousDeepest: 8, currentDeepest: 8));
        Assert.Empty(ShopWares.NewUnlocksBetween(previousDeepest: 10, currentDeepest: 6)); // climbed back up
    }

    [Fact]
    public void The_session_shop_reflects_the_deepest_floor_reached()
    {
        var session = new GameSession(seed: 3);
        Assert.DoesNotContain(Items.AmuletOfVitality, session.ShopStock);

        session.Stats.DeepestDepth = 16;
        Assert.Contains(Items.AmuletOfVitality, session.ShopStock); // restocked on progress
    }
}
