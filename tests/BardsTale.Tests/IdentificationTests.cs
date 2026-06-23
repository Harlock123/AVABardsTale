using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Items;
using BardsTale.Core.Persistence;
using BardsTale.Core.Town;
using BardsTale.Core.Util;
using BardsTale.Desktop.ViewModels;
using Xunit;

namespace BardsTale.Tests;

public class IdentificationTests
{
    private static readonly Item UnknownSword = Items.Enchant(Items.LongSword, 2).AsUnidentified();

    [Fact]
    public void An_unidentified_item_hides_its_name_until_revealed()
    {
        Assert.False(UnknownSword.Identified);
        Assert.Equal("Unidentified Weapon", UnknownSword.DisplayName);

        var revealed = UnknownSword.Identify();
        Assert.True(revealed.Identified);
        Assert.Equal("Long Sword +2", revealed.Name);
        Assert.Equal("Long Sword +2", revealed.DisplayName);
    }

    [Fact]
    public void Magic_drops_arrive_unidentified()
    {
        var rng = new SystemRandomSource(seed: 7);
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Skeleton, 3) });

        Item? magic = null;
        for (var i = 0; i < 2000 && magic is null; i++)
            magic = Loot.Roll(encounter, rng).FirstOrDefault(it => it.IsMagic);

        Assert.NotNull(magic);
        Assert.False(magic!.Identified);
    }

    private static TownViewModel ShopCarrying(out GameSession session, Item item)
    {
        session = new GameSession(seed: 2);
        session.FillDefaultParty();
        session.Party.Gold = 500;
        session.Party.Inventory.Add(item);
        session.TownPosition = session.Town.Buildings.First(b => b.Building == TownBuilding.Shop).Position;
        var town = new TownViewModel(session);
        town.EnterCommand.Execute(null);
        return town;
    }

    [Fact]
    public void Appraising_reveals_a_stashed_item_for_a_fee()
    {
        var town = ShopCarrying(out var session, UnknownSword);
        town.SelectedStashItem = town.Stash.First();
        var goldBefore = town.Gold;

        town.IdentifyCommand.Execute(null);

        Assert.Equal(goldBefore - town.IdentifyCost, town.Gold);
        Assert.Contains(session.Party.Inventory, i => i.Identified && i.Name == "Long Sword +2");
        Assert.DoesNotContain(session.Party.Inventory, i => !i.Identified);
    }

    [Fact]
    public void An_unidentified_item_cannot_be_equipped()
    {
        var town = ShopCarrying(out var session, UnknownSword);
        var warrior = town.Party.First(h => h.Model.Class == CharacterClass.Warrior);
        var oldWeapon = warrior.Model.Weapon;
        town.SelectedHero = warrior;
        town.SelectedStashItem = town.Stash.First();

        town.EquipFromStashCommand.Execute(null);

        Assert.Equal(oldWeapon, warrior.Model.Weapon);          // unchanged
        Assert.Contains(UnknownSword, session.Party.Inventory); // still in the stash
    }

    [Fact]
    public void Unidentified_items_survive_a_save_round_trip()
    {
        var session = new GameSession(seed: 2);
        session.FillDefaultParty();
        session.Party.Inventory.Add(UnknownSword);

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session));
        var item = loaded.Party.Inventory.Single(i => i.Name == "Long Sword +2");

        Assert.False(item.Identified);
        Assert.Equal("Unidentified Weapon", item.DisplayName);
    }
}
