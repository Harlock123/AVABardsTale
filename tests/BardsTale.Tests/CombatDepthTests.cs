using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Magic;
using BardsTale.Core.Util;
using BardsTale.Desktop.ViewModels;
using Xunit;

namespace BardsTale.Tests;

public class SpellProgressionTests
{
    [Fact]
    public void Level1_magician_knows_only_level1_spells()
    {
        var rng = new SystemRandomSource(seed: 4);
        var mage = new CharacterFactory(rng).Create("Vex", Race.Gnome, CharacterClass.Magician);

        var spells = mage.KnownSpells.Select(Spells.Get).ToList();
        Assert.NotEmpty(spells);
        Assert.All(spells, s => Assert.Equal(1, s.Level));
        Assert.All(spells, s => Assert.Equal(MagicSchool.Magician, s.School));
    }

    [Fact]
    public void Advancing_a_caster_teaches_new_spells()
    {
        var rng = new SystemRandomSource(seed: 4);
        var mage = new CharacterFactory(rng).Create("Vex", Race.Gnome, CharacterClass.Magician);
        var before = mage.KnownSpells.Count;

        mage.Experience = mage.ExperienceForNextLevel;
        var message = Progression.TryLevelUp(mage, rng);

        Assert.NotNull(message);
        Assert.Equal(2, mage.Level);
        Assert.True(mage.KnownSpells.Count > before, "a magician should learn a new spell at level 2");
        Assert.Contains(mage.KnownSpells.Select(Spells.Get), s => s.Level == 2);
    }

    [Fact]
    public void Bard_starts_with_songs()
    {
        var rng = new SystemRandomSource(seed: 4);
        var bard = new CharacterFactory(rng).Create("Lyric", Race.HalfElf, CharacterClass.Bard);
        Assert.True(bard.CanSing);
        Assert.NotEmpty(bard.KnownSongs);
    }
}

public class BuffMechanicTests
{
    [Fact]
    public void A_protection_song_lets_a_wounded_party_survive_longer()
    {
        // Deterministic comparison: same seed, with and without a defensive song,
        // the warded run should leave the party with at least as much health.
        int RunFight(bool sing)
        {
            var rng = new SystemRandomSource(seed: 123);
            var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
            var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Berserker, 3) });
            var engine = new CombatEngine(party, encounter, rng, surprise: SurpriseState.None);
            var bard = party.Members.First(m => m.CanSing);
            var song = Songs.Get(bard.KnownSongs.First(id => Songs.Get(id).Effect == SongEffect.BuffPartyArmor));

            for (var round = 0; round < 3 && !engine.IsOver; round++)
            {
                var cmds = party.Members.Where(m => m.CanAct)
                    .Select(m => m == bard && sing
                        ? new CombatCommand(m, CombatActionType.Sing, Song: song)
                        : new CombatCommand(m, CombatActionType.Defend))
                    .ToList();
                engine.ExecuteRound(cmds);
            }
            return party.Members.Sum(m => m.HitPoints);
        }

        Assert.True(RunFight(sing: true) >= RunFight(sing: false));
    }
}

public class CombatViewModelTests
{
    [Fact]
    public void First_actor_has_options_and_a_prompt()
    {
        var rng = new SystemRandomSource(seed: 8);
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 8));
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) });
        var vm = new CombatViewModel(party, encounter, rng, surprise: SurpriseState.None);
        vm.Begin();

        Assert.NotEmpty(vm.Options);
        Assert.Contains("What will", vm.Prompt);
        Assert.False(vm.IsOver);
    }

    [Fact]
    public void Choosing_an_action_queues_an_order_and_advances()
    {
        var rng = new SystemRandomSource(seed: 8);
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 8));
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Skeleton, 3) });
        var vm = new CombatViewModel(party, encounter, rng, surprise: SurpriseState.None);
        vm.Begin();

        vm.ChooseActionCommand.Execute(vm.Options.First());
        Assert.Single(vm.Orders);
    }

    [Fact]
    public void Auto_resolves_rounds_until_combat_ends()
    {
        var rng = new SystemRandomSource(seed: 8);
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 8));
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) });
        var vm = new CombatViewModel(party, encounter, rng, surprise: SurpriseState.None);
        vm.Begin();

        CombatOutcome? outcome = null;
        vm.Finished += (o, _) => outcome = o;

        var safety = 0;
        while (!vm.IsOver && safety++ < 100)
            vm.AutoCommand.Execute(null);

        Assert.True(vm.IsOver);
        Assert.Equal(CombatOutcome.Victory, outcome);
    }
}
