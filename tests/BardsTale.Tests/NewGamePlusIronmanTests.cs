using System.Linq;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Persistence;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Covers New Game+ (carry the party forward into a scaled-up world) and the Ironman flag,
/// including the ascension difficulty scaling and that both survive save/load.
/// </summary>
public class NewGamePlusIronmanTests
{
    [Fact]
    public void Ascension_scaling_toughens_and_enriches_a_monster()
    {
        var t = Bestiary.Ogre;

        Assert.Equal(t, NgPlus.Scale(t, 0)); // a first playthrough is unscaled

        var s = NgPlus.Scale(t, 2);
        Assert.True(s.MaxHitPoints > t.MaxHitPoints, "ascension should add HP");
        Assert.True(s.AttackBonus > t.AttackBonus, "ascension should add to hit/damage");
        Assert.True(s.ExperienceValue > t.ExperienceValue, "ascension should be worth more XP");
        Assert.True(s.GoldValue > t.GoldValue, "ascension should be worth more gold");
        Assert.Equal(t.Name, s.Name); // identity (and elemental affinities) preserved
    }

    [Fact]
    public void A_boss_is_scaled_up_in_new_game_plus()
    {
        var baseBoss = Bosses.Create(1, ascension: 0).Groups[0].Monsters[0];
        var scaledBoss = Bosses.Create(1, ascension: 3).Groups[0].Monsters[0];

        Assert.True(scaledBoss.Template.MaxHitPoints > baseBoss.Template.MaxHitPoints);
        Assert.Equal(scaledBoss.Template.MaxHitPoints, scaledBoss.HitPoints); // spawned at the scaled max
    }

    [Fact]
    public void New_game_plus_carries_the_party_and_raises_ascension()
    {
        var s = new GameSession(seed: 1);
        s.FillDefaultParty();
        s.Party.Gold = 500;
        s.Party.Keys = 2;
        s.Ascension = 1;
        s.Ironman = true;
        var carried = s.Party.Members.ToList();
        foreach (var m in carried) m.HitPoints = 1; // wounded going into the new run

        var ng = s.StartNewGamePlus();

        Assert.Equal(2, ng.Ascension);                       // ascension climbs
        Assert.True(ng.Ironman);                             // the Ironman flag carries over
        Assert.Equal(carried.Count, ng.Party.Members.Count); // the heroes come along
        Assert.Equal(500, ng.Party.Gold);                    // and their purse
        Assert.False(ng.HasActiveDungeon);                   // but the dungeon resets
        Assert.Equal(0, ng.Party.Keys);                      // carried keys do not
        Assert.All(ng.Party.Members, m => Assert.Equal(m.EffectiveMaxHitPoints, m.HitPoints)); // fully rested
    }

    [Fact]
    public void Ascension_and_ironman_survive_save_load()
    {
        var s = new GameSession(seed: 5);
        s.FillDefaultParty();
        s.Ascension = 3;
        s.Ironman = true;

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(s));

        Assert.Equal(3, loaded.Ascension);
        Assert.True(loaded.Ironman);
    }
}
