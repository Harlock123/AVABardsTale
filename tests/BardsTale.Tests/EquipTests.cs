using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Game;
using BardsTale.Core.Items;
using BardsTale.Core.Town;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

public class EquipTests
{
    private static TownViewModel ShopWith(out GameSession session, params Item[] stashItems)
    {
        session = new GameSession(seed: 2);
        session.FillDefaultParty();
        foreach (var i in stashItems)
            session.Party.Inventory.Add(i);
        session.TownPosition = session.Town.Buildings.First(b => b.Building == TownBuilding.Shop).Position;

        var town = new TownViewModel(session);
        town.EnterCommand.Execute(null); // enter Garth's, which rebuilds the stash
        return town;
    }

    [Fact]
    public void The_stash_lists_gear_but_not_consumables()
    {
        var town = ShopWith(out _, Items.LongSword, Items.HealingPotion);

        Assert.True(town.IsShop);
        Assert.Contains(town.Stash, s => s.Item == Items.LongSword);
        Assert.DoesNotContain(town.Stash, s => s.Item == Items.HealingPotion);
    }

    [Fact]
    public void Equipping_from_the_stash_swaps_gear_and_stows_the_old_piece()
    {
        var town = ShopWith(out var session, Items.BattleAxe);

        var warrior = town.Party.First(h => h.Model.Class == CharacterClass.Warrior);
        var oldWeapon = warrior.Model.Weapon;
        town.SelectedHero = warrior;
        town.SelectedStashItem = town.Stash.First(s => s.Item == Items.BattleAxe);

        town.EquipFromStashCommand.Execute(null);

        Assert.Equal(Items.BattleAxe, warrior.Model.Weapon);
        Assert.DoesNotContain(Items.BattleAxe, session.Party.Inventory);
        Assert.Contains(oldWeapon!, session.Party.Inventory); // displaced weapon returns to the stash
    }

    [Fact]
    public void A_mage_cannot_equip_plate_from_the_stash()
    {
        var town = ShopWith(out var session, Items.PlateMail);

        var mage = town.Party.First(h => h.Model.Class == CharacterClass.Magician);
        town.SelectedHero = mage;
        town.SelectedStashItem = town.Stash.First(s => s.Item == Items.PlateMail);

        town.EquipFromStashCommand.Execute(null);

        Assert.NotEqual(Items.PlateMail, mage.Model.Armor);
        Assert.Contains(Items.PlateMail, session.Party.Inventory); // stays in the stash, unequipped
    }
}
