using System.Linq;
using BardsTale.Core.Items;
using BardsTale.Core.Magic;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

/// <summary>Covers the in-combat spell picker's cost/affordability cues (caster QoL parity).</summary>
public class CombatOptionTests
{
    [Fact]
    public void A_cast_option_shows_its_cost_when_affordable()
    {
        var spell = Spells.All.First(s => s.UsableInCombat && s.Cost > 0);
        var option = CombatActionOptionViewModel.Cast(spell, casterSp: spell.Cost);

        Assert.True(option.ShowsCost);
        Assert.True(option.CanAfford);
        Assert.Equal($"{spell.Cost} SP", option.CostText);
    }

    [Fact]
    public void A_cast_option_the_caster_cannot_afford_is_flagged()
    {
        var spell = Spells.All.First(s => s.UsableInCombat && s.Cost > 0);
        var option = CombatActionOptionViewModel.Cast(spell, casterSp: spell.Cost - 1);

        Assert.False(option.CanAfford);
        Assert.True(option.ShowsCost); // still shown — just dimmed, not hidden
    }

    [Fact]
    public void A_free_item_power_option_shows_no_spell_cost()
    {
        var option = CombatActionOptionViewModel.UsePower(Items.WandOfFlames);

        Assert.False(option.ShowsCost);
        Assert.True(option.CanAfford);
        Assert.Equal("", option.CostText);
    }

    [Fact]
    public void Plain_actions_carry_no_cost()
    {
        Assert.False(CombatActionOptionViewModel.Attack().ShowsCost);
        Assert.False(CombatActionOptionViewModel.Defend().ShowsCost);
    }
}
