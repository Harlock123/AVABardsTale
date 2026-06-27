using BardsTale.Core.Combat;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

/// <summary>Covers the combat-juice triggers: a struck enemy group flashes and pops a number.</summary>
public class CombatJuiceTests
{
    private static MonsterGroupViewModel Group() =>
        new(new MonsterGroup(Bestiary.Goblin, 2), 0);

    [Fact]
    public void A_struck_group_flashes_and_pops_a_number()
    {
        var g = Group();
        g.Pop(-5); // a blow lands

        Assert.True(g.HitFlashPulse > 0, "a struck group should flash");
        Assert.True(g.FloatingPulse > 0, "and pop a damage number");
    }

    [Fact]
    public void A_mended_group_pops_a_number_but_does_not_flash()
    {
        var g = Group();
        g.Pop(8); // healed

        Assert.Equal(0, g.HitFlashPulse);          // healing is no impact — no flash
        Assert.True(g.FloatingPulse > 0);          // but the "+8" still pops
    }

    [Fact]
    public void A_zero_delta_does_nothing()
    {
        var g = Group();
        g.Pop(0);

        Assert.Equal(0, g.HitFlashPulse);
        Assert.Equal(0, g.FloatingPulse);
    }
}
