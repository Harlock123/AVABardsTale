using System;
using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Geometry;
using BardsTale.Core.Items;
using BardsTale.Core.Persistence;
using BardsTale.Core.Util;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

public class BankSellTests
{
    [Fact]
    public void Bank_deposit_and_withdraw_move_gold_between_purse_and_vault()
    {
        var session = new GameSession(seed: 2);
        session.FillDefaultParty();
        session.Party.Gold = 1000;
        session.Party.BankedGold = 0;
        var vm = new TownViewModel(session);

        vm.BankAmount = 300;
        vm.DepositCommand.Execute(null);
        Assert.Equal(700, session.Party.Gold);
        Assert.Equal(300, session.Party.BankedGold);

        vm.BankAmount = 100;
        vm.WithdrawCommand.Execute(null);
        Assert.Equal(800, session.Party.Gold);
        Assert.Equal(200, session.Party.BankedGold);

        vm.DepositAllCommand.Execute(null);
        Assert.Equal(0, session.Party.Gold);
        Assert.Equal(1000, session.Party.BankedGold);
    }

    [Fact]
    public void You_cannot_deposit_more_than_you_carry()
    {
        var session = new GameSession(seed: 2);
        session.FillDefaultParty();
        session.Party.Gold = 50;
        session.Party.BankedGold = 0;
        var vm = new TownViewModel(session);

        vm.BankAmount = 9999;
        vm.DepositCommand.Execute(null);
        Assert.Equal(0, session.Party.Gold);   // only what was carried
        Assert.Equal(50, session.Party.BankedGold);
    }

    [Fact]
    public void Banked_gold_is_never_touched_by_combat_so_thieves_cant_reach_it()
    {
        var rng = new SystemRandomSource(seed: 1);
        var party = NewGame.CreateDefaultParty(rng);
        party.Gold = 100;
        party.BankedGold = 500;

        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Cutpurse, 3) });
        var engine = new CombatEngine(party, encounter, rng, surprise: SurpriseState.PartySurprised);
        engine.ExecuteRound(Array.Empty<CombatCommand>()); // cutpurses get a free round to filch

        Assert.Equal(500, party.BankedGold); // the vault is untouched no matter what they steal
    }

    [Fact]
    public void Banked_gold_round_trips_through_a_save()
    {
        var session = new GameSession(seed: 2);
        session.FillDefaultParty();
        session.Party.Gold = 100;
        session.Party.BankedGold = 750;

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session));
        Assert.Equal(750, loaded.Party.BankedGold);
        Assert.Equal(100, loaded.Party.Gold);
    }

    [Fact]
    public void Selling_a_stashed_item_pays_half_its_value()
    {
        var session = new GameSession(seed: 2);
        session.FillDefaultParty();
        session.Party.Gold = 0;
        session.Party.Inventory.Add(Items.LongSword); // value 120
        session.TownPosition = new Position(10, 5);    // Garth's Equipment Shoppe
        var vm = new TownViewModel(session);
        vm.EnterCommand.Execute(null);
        Assert.True(vm.IsShop);

        vm.SelectedStashItem = vm.Stash.First();
        vm.SellStashItemCommand.Execute(null);

        Assert.Equal(60, session.Party.Gold); // half of 120
        Assert.DoesNotContain(Items.LongSword, session.Party.Inventory);
    }
}
