using BardsTale.Core.Characters;
using BardsTale.Core.Game;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Level-up rewards beyond HP/SP: a prime-attribute point on milestone levels, and the
/// extra-attack call-out when a martial class crosses an attacks-per-round threshold.
/// </summary>
public class ProgressionTests
{
    private static Character Hero(CharacterClass cls, out SystemRandomSource rng, int xp = 100_000_000)
    {
        rng = new SystemRandomSource(seed: 1);
        var hero = new CharacterFactory(rng).Create("Hero", Race.Human, cls);
        hero.Experience = xp;
        return hero;
    }

    [Fact]
    public void A_milestone_level_grants_a_prime_attribute_point()
    {
        var hero = Hero(CharacterClass.Warrior, out var rng);
        var before = hero.Attributes.Strength;
        while (hero.Level < 3) Progression.TryLevelUp(hero, rng);
        Assert.True(hero.Attributes.Strength > before, "a Warrior should gain Strength at level 3");
    }

    [Fact]
    public void A_non_milestone_level_grants_no_attribute_point()
    {
        var hero = Hero(CharacterClass.Warrior, out var rng);
        var before = hero.Attributes.Strength;
        Progression.TryLevelUp(hero, rng); // → level 2, not a milestone
        Assert.Equal(2, hero.Level);
        Assert.Equal(before, hero.Attributes.Strength);
    }

    [Fact]
    public void A_caster_advances_intelligence_on_milestones()
    {
        var rng = new SystemRandomSource(seed: 2);
        var mage = new CharacterFactory(rng).Create("Vex", Race.Gnome, CharacterClass.Magician);
        mage.Experience = 100_000;
        var before = mage.Attributes.Intelligence;
        while (mage.Level < 3) Progression.TryLevelUp(mage, rng);
        Assert.True(mage.Attributes.Intelligence > before, "a Magician should gain Intelligence at level 3");
    }

    [Fact]
    public void Crossing_an_extra_attack_threshold_is_announced()
    {
        var monk = Hero(CharacterClass.Monk, out var rng); // divisor 8 → a 2nd attack at level 8
        string? crossing = null;
        while (monk.Level < 8)
        {
            var msg = Progression.TryLevelUp(monk, rng);
            if (msg is not null && msg.Contains("extra attack")) crossing = msg;
        }
        Assert.NotNull(crossing);
        Assert.Equal(2, monk.AttacksPerRound);
    }
}
