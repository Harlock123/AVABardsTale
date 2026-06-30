using System.Linq;
using BardsTale.Core.Game;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// The Review Board's per-hero "Advance" button — its enabled state is driven by the command's
/// CanExecute, so only heroes who can actually level up are clickable (the rest stay disabled).
/// </summary>
public class ReviewBoardTests
{
    [Fact]
    public void Advance_is_executable_only_for_a_hero_who_can_level()
    {
        var session = new GameSession(seed: 3);
        session.FillDefaultParty();
        var town = new TownViewModel(session);

        // Bank exactly enough experience for the first hero, and none for the rest.
        var ready = session.Party.Members[0];
        ready.Experience = ready.ExperienceForNextLevel;

        var readyVm = town.Party.First(v => ReferenceEquals(v.Model, ready));
        var notReadyVm = town.Party.First(v => !ReferenceEquals(v.Model, ready));

        Assert.True(town.AdvanceCommand.CanExecute(readyVm), "a hero with the XP should be advanceable");
        Assert.False(town.AdvanceCommand.CanExecute(notReadyVm), "a hero without the XP should not be");
        Assert.False(town.AdvanceCommand.CanExecute(null));
    }

    [Fact]
    public void Advancing_a_hero_levels_them_and_updates_eligibility()
    {
        var session = new GameSession(seed: 3);
        session.FillDefaultParty();
        var town = new TownViewModel(session);

        var hero = session.Party.Members[0];
        hero.Experience = hero.ExperienceForNextLevel; // enough for exactly one level
        var heroVm = town.Party.First(v => ReferenceEquals(v.Model, hero));
        var startLevel = hero.Level;

        Assert.True(town.AdvanceCommand.CanExecute(heroVm));
        town.AdvanceCommand.Execute(heroVm);

        Assert.Equal(startLevel + 1, hero.Level);
        Assert.False(town.AdvanceCommand.CanExecute(heroVm), "spent XP — no longer eligible");
    }
}
