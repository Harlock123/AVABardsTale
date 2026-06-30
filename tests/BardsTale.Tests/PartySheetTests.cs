using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Items;
using BardsTale.Core.Util;
using BardsTale.UI.ViewModels;
using Xunit;
using Attribute = BardsTale.Core.Characters.Attribute;

namespace BardsTale.Tests;

/// <summary>The character-sheet overlay VM — surfacing every hero's derived numbers.</summary>
public class PartySheetTests
{
    [Fact]
    public void Sheet_lists_every_party_member()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var sheet = new PartySheetViewModel(party);
        Assert.Equal(party.Members.Count, sheet.Heroes.Count);
    }

    [Fact]
    public void A_hero_card_shows_vitals_attributes_and_marks_the_prime()
    {
        var rng = new SystemRandomSource(seed: 1);
        var warrior = new CharacterFactory(rng).Create("Bryn", Race.Dwarf, CharacterClass.Warrior);
        var party = new Party();
        party.Add(warrior);

        var card = new PartySheetViewModel(party).Heroes[0];

        Assert.Contains("Bryn", card.Header);
        Assert.Contains("HP", card.Vitals);
        Assert.Contains("Attacks/round", card.Vitals);
        Assert.Equal(5, card.Attributes.Count);
        // A Warrior's prime is Strength — exactly one chip is highlighted, and it's the ST one.
        var prime = card.Attributes.Single(a => a.IsPrime);
        Assert.StartsWith(AttributeSet.Abbreviation(Attribute.Strength), prime.Text);
    }

    [Fact]
    public void The_boon_line_reflects_an_active_party_boon()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));

        Assert.False(new PartySheetViewModel(party).HasBoon);

        party.Boon = PartyBoon.Might;
        var sheet = new PartySheetViewModel(party);
        Assert.True(sheet.HasBoon);
        Assert.Contains("Might", sheet.BoonLine);
    }

    [Fact]
    public void Wards_reflect_a_heros_resisted_elements()
    {
        var rng = new SystemRandomSource(seed: 1);
        var hero = new CharacterFactory(rng).Create("Vex", Race.Human, CharacterClass.Warrior);
        var party = new Party();
        party.Add(hero);

        Assert.Equal("Wards  none", new PartySheetViewModel(party).Heroes[0].Wards);

        hero.Ring1 = Items.RingOfFireWard;
        var card = new PartySheetViewModel(party).Heroes[0];
        Assert.True(card.HasWards);
        Assert.Contains("Fire", card.Wards);
    }
}
