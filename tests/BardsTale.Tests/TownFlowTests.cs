using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Game;
using BardsTale.Core.Items;
using BardsTale.Desktop.ViewModels;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Exercises the town/session view-model flow that drives the UI, without a
/// running Avalonia app. These are the real types the views bind to.
/// </summary>
public class SessionTests
{
    [Fact]
    public void FillDefaultParty_recruits_six_and_pools_gold()
    {
        var session = new GameSession(seed: 3);
        session.FillDefaultParty();

        Assert.Equal(6, session.Party.Members.Count);
        Assert.True(session.Party.Gold > 0, "starting gold should be pooled into the purse");
        Assert.All(session.Party.Members, m => Assert.Equal(0, m.Gold));
    }

    [Fact]
    public void EnterDungeon_resumes_same_dungeon_at_entrance()
    {
        var session = new GameSession(seed: 3);
        session.FillDefaultParty();

        var first = session.EnterDungeon();
        first.StepForward();
        var moved = session.Party.Position;

        var second = session.EnterDungeon();
        Assert.Same(first, second);
        Assert.Equal(second.Maze.StartPosition, session.Party.Position);
    }

    [Fact]
    public void Buying_heavy_armor_is_refused_for_a_mage()
    {
        var session = new GameSession(seed: 3);
        var mage = session.Factory.Create("Vex", Race.Gnome, CharacterClass.Magician);
        Assert.False(Equipment.CanEquip(mage, Items.PlateMail, out var reason));
        Assert.False(string.IsNullOrEmpty(reason));
    }
}

public class TownViewModelTests
{
    [Fact]
    public void QuickParty_enables_entering_the_dungeon()
    {
        var session = new GameSession(seed: 11);
        var town = new TownViewModel(session);

        Assert.False(town.EnterDungeonCommand.CanExecute(null));
        town.QuickPartyCommand.Execute(null);

        Assert.Equal(6, town.Party.Count);
        Assert.True(town.EnterDungeonCommand.CanExecute(null));
        Assert.True(town.Gold > 0);
    }

    [Fact]
    public void Recruiting_a_created_hero_adds_them_to_the_party()
    {
        var session = new GameSession(seed: 11);
        var town = new TownViewModel(session);

        town.Creation.Name = "Tester";
        town.Creation.RollCommand.Execute(null);
        Assert.True(town.Creation.HasCandidate);

        town.RecruitCommand.Execute(null);
        Assert.Single(town.Party);
        Assert.False(town.Creation.HasCandidate); // draft reset after recruiting
    }

    [Fact]
    public void ReviewBoard_advances_a_hero_with_enough_experience()
    {
        var session = new GameSession(seed: 11);
        var town = new TownViewModel(session);
        town.QuickPartyCommand.Execute(null);

        var hero = town.Party.First();
        hero.Model.Experience = hero.Model.ExperienceForNextLevel;
        hero.Refresh();
        Assert.True(hero.CanLevelUp);

        var before = hero.Model.Level;
        town.AdvanceCommand.Execute(hero);
        Assert.Equal(before + 1, hero.Model.Level);
    }
}

public class NavigationTests
{
    [Fact]
    public void Starts_in_town_then_switches_to_exploration_and_back()
    {
        var main = new MainWindowViewModel();
        var town = Assert.IsType<TownViewModel>(main.CurrentView);

        town.QuickPartyCommand.Execute(null);
        town.EnterDungeonCommand.Execute(null);

        var exploration = Assert.IsType<ExplorationViewModel>(main.CurrentView);
        Assert.NotNull(main.Exploration);

        exploration.ReturnToTownCommand.Execute(null);
        Assert.IsType<TownViewModel>(main.CurrentView);
    }
}
