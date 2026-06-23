using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Items;
using BardsTale.Core.Magic;
using BardsTale.Core.Town;
using BardsTale.Core.Util;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

public class RogueSpellIdentifyTests
{
    private static readonly Item UnknownSword = Items.Enchant(Items.LongSword, 2).AsUnidentified();

    private static TownViewModel ShopCarrying(out GameSession session, Item item)
    {
        session = new GameSession(seed: 2);
        session.FillDefaultParty();
        session.Party.Inventory.Add(item);
        session.TownPosition = session.Town.Buildings.First(b => b.Building == TownBuilding.Shop).Position;
        var town = new TownViewModel(session);
        town.EnterCommand.Execute(null);
        town.SelectedStashItem = town.Stash.First();
        return town;
    }

    [Fact]
    public void Rogue_skill_improves_with_level_and_is_capped()
    {
        Assert.True(Identification.RogueChance(5) > Identification.RogueChance(1));
        Assert.True(Identification.RogueChance(100) <= 0.95);
    }

    [Fact]
    public void A_rogue_can_appraise_for_free()
    {
        var town = ShopCarrying(out var session, UnknownSword);
        town.Party.First(h => h.Model.Class == CharacterClass.Rogue).Model.Level = 12; // high skill
        var goldBefore = town.Gold;

        var safety = 0;
        while (session.Party.Inventory.Any(i => !i.Identified) && safety++ < 100)
            town.IdentifyWithRogueCommand.Execute(null);

        Assert.Contains(session.Party.Inventory, i => i.Identified && i.Name == "Long Sword +2");
        Assert.Equal(goldBefore, town.Gold); // the rogue charges nothing
    }

    [Fact]
    public void Without_a_rogue_the_skill_route_does_nothing()
    {
        var town = ShopCarrying(out var session, UnknownSword);
        foreach (var rogue in town.Party.Where(h => h.Model.Class == CharacterClass.Rogue).ToList())
            session.Party.Remove(rogue.Model);

        town.IdentifyWithRogueCommand.Execute(null);

        Assert.DoesNotContain(session.Party.Inventory, i => i.Identified && i.Name == "Long Sword +2");
    }

    [Fact]
    public void Scrye_sight_identifies_reliably_for_spell_points()
    {
        var town = ShopCarrying(out var session, UnknownSword);
        var mage = session.Party.Members.First(m => m.KnownSpells.Contains("SCSI"));
        var spBefore = mage.SpellPoints;

        town.IdentifyWithMagicCommand.Execute(null);

        Assert.Contains(session.Party.Inventory, i => i.Identified && i.Name == "Long Sword +2");
        Assert.True(mage.SpellPoints < spBefore);
    }

    [Fact]
    public void The_identify_spell_is_not_offered_in_combat()
    {
        Assert.False(Spells.Get("SCSI").UsableInCombat);

        var factory = new CharacterFactory(new SystemRandomSource(seed: 1));
        var party = new Party();
        party.Add(factory.Create("Vex", Race.Gnome, CharacterClass.Magician));

        var vm = new CombatViewModel(party, new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) }),
            new SystemRandomSource(seed: 1), surprise: SurpriseState.None);
        vm.Begin();

        Assert.DoesNotContain(vm.Options, o => o.Spell?.Effect == SpellEffect.Identify);
    }
}
