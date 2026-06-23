using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Items;
using BardsTale.Core.Util;
using BardsTale.Desktop.ViewModels;
using Xunit;

namespace BardsTale.Tests;

public class LootTests
{
    [Fact]
    public void Loot_only_drops_known_items()
    {
        var rng = new SystemRandomSource(seed: 7);
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Skeleton, 3) });
        var knownNames = Items.Potions
            .Concat(new[] { Items.ShortSword, Items.LongSword, Items.LeatherArmor, Items.ChainMail, Items.SmallShield })
            .Concat(Items.MagicItems)
            .Select(i => i.Name)
            .ToHashSet();

        var dropped = false;
        for (var i = 0; i < 300; i++)
            foreach (var item in Loot.Roll(encounter, rng))
            {
                Assert.Contains(item.Name, knownNames); // real name is known even when unidentified
                dropped = true;
            }

        Assert.True(dropped, "300 rolls should yield at least one drop");
    }

    [Fact]
    public void Victories_accumulate_loot_in_the_party_stash()
    {
        var game = NewGame.CreateDefault(seed: 7);
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Skeleton, 3) });

        for (var i = 0; i < 40; i++)
            game.ApplyVictory(encounter);

        Assert.NotEmpty(game.Party.Inventory);
    }
}

public class PotionCombatTests
{
    private static (Party party, Character user, Character patient) MakeParty()
    {
        var factory = new CharacterFactory(new SystemRandomSource(seed: 2));
        var party = new Party();
        var user = factory.Create("User", Race.Dwarf, CharacterClass.Warrior);
        user.MaxHitPoints = 30;
        user.HitPoints = 30;
        var patient = factory.Create("Patient", Race.Human, CharacterClass.Rogue);
        patient.MaxHitPoints = 40;
        patient.HitPoints = 5;
        party.Add(user);
        party.Add(patient);
        return (party, user, patient);
    }

    [Fact]
    public void Using_a_healing_potion_heals_the_target_and_consumes_it()
    {
        var (party, user, patient) = MakeParty();
        party.Inventory.Add(Items.HealingPotion);

        var engine = new CombatEngine(party, new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) }),
            new SystemRandomSource(seed: 2), surprise: SurpriseState.None);

        engine.ExecuteRound(new[]
        {
            new CombatCommand(user, CombatActionType.UseItem, Item: Items.HealingPotion, TargetAllyIndex: 1),
            new CombatCommand(patient, CombatActionType.Defend)
        });

        Assert.True(patient.HitPoints > 5);
        Assert.DoesNotContain(Items.HealingPotion, party.Inventory);
    }

    [Fact]
    public void Resurrection_dust_revives_a_fallen_ally()
    {
        var (party, user, patient) = MakeParty();
        patient.ApplyDamage(patient.MaxHitPoints + 10);
        Assert.True(patient.IsDead);
        party.Inventory.Add(Items.ResurrectionDust);

        var engine = new CombatEngine(party, new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) }),
            new SystemRandomSource(seed: 2), surprise: SurpriseState.None);

        engine.ExecuteRound(new[]
        {
            new CombatCommand(user, CombatActionType.UseItem, Item: Items.ResurrectionDust, TargetAllyIndex: 1)
        });

        Assert.False(patient.IsDead);
        Assert.DoesNotContain(Items.ResurrectionDust, party.Inventory);
    }

    [Fact]
    public void A_consumable_appears_as_a_combat_option_and_needs_a_target()
    {
        var (party, _, _) = MakeParty();
        party.Inventory.Add(Items.HealingPotion);

        var vm = new CombatViewModel(party, new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) }),
            new SystemRandomSource(seed: 2), surprise: SurpriseState.None);
        vm.Begin();

        var useOption = vm.Options.FirstOrDefault(o => o.Action == CombatActionType.UseItem);
        Assert.NotNull(useOption);
        Assert.True(useOption!.NeedsAllyTarget);

        vm.ChooseActionCommand.Execute(useOption);
        Assert.True(vm.IsChoosingTarget);
        Assert.NotEmpty(vm.AllyTargets);
    }
}

public class ShopConsumableTests
{
    [Fact]
    public void Buying_a_potion_adds_it_to_the_party_stash()
    {
        var session = new GameSession(seed: 2);
        session.FillDefaultParty();
        var town = new TownViewModel(session);

        town.SelectedItem = town.Stock.First(s => s.Item == Items.HealingPotion);
        var goldBefore = town.Gold;
        town.BuyCommand.Execute(null);

        Assert.Contains(Items.HealingPotion, session.Party.Inventory);
        Assert.Equal(goldBefore - Items.HealingPotion.Value, town.Gold);
    }
}
