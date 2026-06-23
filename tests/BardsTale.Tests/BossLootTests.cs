using System.Linq;
using BardsTale.Core.Combat;
using BardsTale.Core.Items;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

public class BossLootTests
{
    [Fact]
    public void A_boss_always_drops_a_magic_item()
    {
        var drops = Loot.Roll(Bosses.Create(1), new SystemRandomSource(seed: 1));
        Assert.Contains(drops, i => i.IsMagic);
    }

    [Fact]
    public void Mangar_drops_his_signature_staff()
    {
        var drops = Loot.Roll(Bosses.Create(Bosses.FinalDepth), new SystemRandomSource(seed: 1));
        Assert.Contains(Items.MangarsStaff, drops);
    }

    [Fact]
    public void The_signature_staff_is_a_potent_identified_weapon_that_saves_resolve()
    {
        var staff = Items.MangarsStaff;
        Assert.True(staff.IsMagic);
        Assert.True(staff.Identified);
        Assert.True(staff.IsWeapon);
        Assert.Equal("Mangar's Staff", staff.DisplayName);
        Assert.Equal(staff, Items.Find("Mangar's Staff")); // resolvable when loading a save
    }

    [Fact]
    public void Ordinary_fights_do_not_get_the_guaranteed_boss_drop()
    {
        var ordinary = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) }); // not a boss
        var drops = Loot.Roll(ordinary, new SystemRandomSource(seed: 4));
        Assert.DoesNotContain(Items.MangarsStaff, drops);
    }
}
