using System.Linq;
using BardsTale.Core.Combat;
using BardsTale.Core.Lore;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Covers the bestiary lore expansion: every monster gets a flavour line and a drop hint,
/// bosses get bespoke lore and a guaranteed-drop hint, and generated lore reflects real traits.
/// </summary>
public class MonsterLoreTests
{
    [Fact]
    public void Every_catalogued_monster_has_flavor_and_a_drop_hint()
    {
        foreach (var t in MonsterCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(MonsterLore.FlavorFor(t)), $"{t.Name} has no flavour");
            Assert.False(string.IsNullOrWhiteSpace(MonsterLore.DropHint(t)), $"{t.Name} has no drop hint");
        }
    }

    [Fact]
    public void Bosses_get_bespoke_lore_and_a_guaranteed_drop_hint()
    {
        Assert.Contains("Mad Wizard", MonsterLore.FlavorFor(Bosses.Mangar));
        Assert.Contains("staff", MonsterLore.DropHint(Bosses.Mangar));

        Assert.Contains("witches", MonsterLore.FlavorFor(Bosses.CovenMatron));     // curated
        Assert.Contains("magic item", MonsterLore.DropHint(Bosses.SkeletonLord));  // lair-boss guaranteed drop
    }

    [Fact]
    public void Generated_lore_reflects_a_monsters_traits()
    {
        Assert.Contains("venom", MonsterLore.FlavorFor(Bestiary.GiantSpider));  // poison
        Assert.Contains("vigour", MonsterLore.FlavorFor(Bestiary.Wight));        // level drain
        Assert.Contains("magic", MonsterLore.FlavorFor(Bestiary.DarkElf));       // a caster
    }

    [Fact]
    public void Drop_hints_scale_with_a_monsters_worth()
    {
        Assert.Contains("nothing of worth", MonsterLore.DropHint(Bestiary.CaveBat));     // 0 gold
        Assert.Contains("fortune", MonsterLore.DropHint(Bestiary.AncientRedDragon));     // 600 gold
    }

    [Fact]
    public void Generated_kinds_read_distinctly()
    {
        // A few non-curated foes should pick up their category flavour.
        Assert.Contains("undead", MonsterLore.FlavorFor(Bestiary.Zombie));
        Assert.Contains("scaled", MonsterLore.FlavorFor(Bestiary.YoungBlueDragon));
        Assert.Contains("construct", MonsterLore.FlavorFor(Bestiary.IronGolem));
    }
}
