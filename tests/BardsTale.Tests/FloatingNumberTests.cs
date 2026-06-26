using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Util;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

/// <summary>Covers the floating-number "pop" state on the roster and enemy-group view models.</summary>
public class FloatingNumberTests
{
    [Fact]
    public void A_hero_pop_shows_damage_as_a_negative_and_bumps_the_pulse()
    {
        var hero = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1)).Members[0];
        var vm = new CharacterViewModel(hero);
        var before = vm.FloatingPulse;

        vm.Pop(-7);

        Assert.Equal("-7", vm.FloatingText);
        Assert.True(vm.FloatingPulse > before);
    }

    [Fact]
    public void A_hero_pop_shows_healing_as_a_positive()
    {
        var hero = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1)).Members[0];
        var vm = new CharacterViewModel(hero);

        vm.Pop(9);

        Assert.Equal("+9", vm.FloatingText);
    }

    [Fact]
    public void A_zero_delta_does_not_pop()
    {
        var hero = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1)).Members[0];
        var vm = new CharacterViewModel(hero);
        var before = vm.FloatingPulse;

        vm.Pop(0);

        Assert.Equal(before, vm.FloatingPulse);
    }

    [Fact]
    public void An_enemy_group_pops_damage_too()
    {
        var vm = new MonsterGroupViewModel(new MonsterGroup(Bestiary.Goblin, 3), 0);
        var before = vm.FloatingPulse;

        vm.Pop(-12);

        Assert.Equal("-12", vm.FloatingText);
        Assert.True(vm.FloatingPulse > before);
    }
}
