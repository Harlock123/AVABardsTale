using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Magic;
using BardsTale.Core.Util;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

public class CombatTargetingTests
{
    private static Party MakeCasterParty(out Character conjurer)
    {
        var factory = new CharacterFactory(new SystemRandomSource(seed: 2));
        var party = new Party();
        conjurer = factory.Create("Mira", Race.Human, CharacterClass.Conjurer);
        conjurer.SpellPoints = 20;
        party.Add(conjurer);
        return party;
    }

    [Fact]
    public void Casting_a_single_ally_spell_opens_target_selection()
    {
        var party = MakeCasterParty(out _);
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) });
        var vm = new CombatViewModel(party, encounter, new SystemRandomSource(seed: 2), surprise: SurpriseState.None);
        vm.Begin();

        var heal = vm.Options.FirstOrDefault(o => o.NeedsAllyTarget);
        Assert.NotNull(heal);

        vm.ChooseActionCommand.Execute(heal);

        Assert.True(vm.IsChoosingTarget);
        Assert.NotEmpty(vm.AllyTargets);
        Assert.All(vm.AllyTargets, t => Assert.True(t.IsValid)); // living member is a valid heal target
    }

    [Fact]
    public void Cancelling_target_selection_returns_to_the_action_list()
    {
        var party = MakeCasterParty(out _);
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) });
        var vm = new CombatViewModel(party, encounter, new SystemRandomSource(seed: 2), surprise: SurpriseState.None);
        vm.Begin();

        vm.ChooseActionCommand.Execute(vm.Options.First(o => o.NeedsAllyTarget));
        Assert.True(vm.IsChoosingTarget);

        vm.CancelTargetCommand.Execute(null);

        Assert.False(vm.IsChoosingTarget);
        Assert.NotEmpty(vm.Options);
    }

    [Fact]
    public void Choosing_a_target_queues_the_spell_and_leaves_targeting()
    {
        var party = MakeCasterParty(out _);
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) });
        var vm = new CombatViewModel(party, encounter, new SystemRandomSource(seed: 2), surprise: SurpriseState.None);
        vm.Begin();

        vm.ChooseActionCommand.Execute(vm.Options.First(o => o.NeedsAllyTarget));
        vm.ChooseAllyTargetCommand.Execute(vm.AllyTargets.First());

        // Sole actor: choosing the target resolves the round and leaves the targeting step.
        Assert.False(vm.IsChoosingTarget);
    }

    [Fact]
    public void A_heal_lands_on_the_chosen_ally_not_another()
    {
        var factory = new CharacterFactory(new SystemRandomSource(seed: 2));
        var party = new Party();

        var conjurer = factory.Create("Mira", Race.Human, CharacterClass.Conjurer);
        conjurer.MaxHitPoints = 30;
        conjurer.HitPoints = 30;
        conjurer.SpellPoints = 20;
        var tank = factory.Create("Tank", Race.Dwarf, CharacterClass.Warrior);
        tank.MaxHitPoints = 40;
        tank.HitPoints = 5;
        party.Add(conjurer);
        party.Add(tank);

        var conjurerStart = conjurer.HitPoints;
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) });
        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 2), surprise: SurpriseState.None);

        engine.ExecuteRound(new[]
        {
            new CombatCommand(conjurer, CombatActionType.CastSpell, Spell: Spells.Get("VOPL"), TargetAllyIndex: 1),
            new CombatCommand(tank, CombatActionType.Defend)
        });

        Assert.True(tank.HitPoints > 5, "the targeted ally should have been healed");
        Assert.True(conjurer.HitPoints <= conjurerStart, "the caster should not have healed itself");
    }
}
