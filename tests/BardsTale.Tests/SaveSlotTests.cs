using System;
using System.IO;
using System.Linq;
using BardsTale.Core.Game;
using BardsTale.Desktop.Services;
using BardsTale.Desktop.ViewModels;
using Xunit;

namespace BardsTale.Tests;

public class SaveSlotTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "BardsTaleTest_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Slots_are_independent_and_describable()
    {
        var saves = new SaveService(_dir);
        var session = new GameSession(seed: 1);
        session.FillDefaultParty();
        session.Party.Gold = 777;

        Assert.False(saves.Exists("slot1"));
        saves.Save(session, "slot1");

        Assert.True(saves.Exists("slot1"));
        Assert.False(saves.Exists("slot2"));

        var desc = saves.Describe("slot1");
        Assert.Contains("heroes", desc);
        Assert.Contains("777", desc);
        Assert.Equal("— empty —", saves.Describe("slot2"));

        Assert.Equal(777, saves.Load("slot1").Party.Gold);
    }

    [Fact]
    public void Saving_to_a_slot_and_loading_it_back_round_trips_through_the_navigator()
    {
        var saves = new SaveService(_dir);
        var main = new MainWindowViewModel(saves);
        ((TownViewModel)main.CurrentView).QuickPartyCommand.Execute(null);

        main.ShowSaveSlotsCommand.Execute(null);
        Assert.True(main.IsSlotPanelOpen);
        Assert.True(main.IsSaveMode);

        main.SaveToSlotCommand.Execute(main.Slots.First(s => s.Slot == "slot2"));
        Assert.True(saves.Exists("slot2"));
        Assert.False(main.IsSlotPanelOpen);

        var reloaded = new MainWindowViewModel(saves);
        reloaded.ShowLoadSlotsCommand.Execute(null);
        var slot2 = reloaded.Slots.First(s => s.Slot == "slot2");
        Assert.True(slot2.IsOccupied);

        reloaded.LoadFromSlotCommand.Execute(slot2);
        Assert.Equal(6, ((TownViewModel)reloaded.CurrentView).Party.Count);
    }

    [Fact]
    public void Returning_to_town_from_the_dungeon_autosaves()
    {
        var saves = new SaveService(_dir);
        var main = new MainWindowViewModel(saves);
        var town = (TownViewModel)main.CurrentView;
        town.QuickPartyCommand.Execute(null);
        town.EnterDungeonCommand.Execute(null);

        Assert.False(saves.Exists(SaveService.AutosaveSlot));

        ((ExplorationViewModel)main.CurrentView).ReturnToTownCommand.Execute(null);

        Assert.True(saves.Exists(SaveService.AutosaveSlot));
        Assert.IsType<TownViewModel>(main.CurrentView);
    }

    [Fact]
    public void The_autosave_slot_cannot_be_written_to_by_hand()
    {
        var saves = new SaveService(_dir);
        var main = new MainWindowViewModel(saves);
        ((TownViewModel)main.CurrentView).QuickPartyCommand.Execute(null);

        main.SaveToSlotCommand.Execute(main.Slots.First(s => s.IsAutosave));

        Assert.False(saves.Exists(SaveService.AutosaveSlot));
    }
}
