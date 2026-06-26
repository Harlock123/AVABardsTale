using Avalonia.Input;
using BardsTale.UI.Settings;
using BardsTale.UI.ViewModels;

namespace BardsTale.UI;

/// <summary>
/// Maps arrow keys / WASD to party movement while exploring the dungeon or the town.
/// Shared by every platform head's root view so keyboard control behaves identically
/// on desktop (a Window) and single-view hosts like the browser (a root control).
/// </summary>
public static class MovementKeys
{
    public static void Handle(MainWindowViewModel vm, KeyEventArgs e)
    {
        // F1 toggles the help/tutorial overlay from anywhere.
        if (e.Key is Key.F1)
        {
            vm.ToggleHelp();
            e.Handled = true;
            return;
        }

        // Escape backs out: close the help overlay, save/load panel, leave a building, etc.
        if (e.Key is Key.Escape)
        {
            if (vm.IsHelpOpen)
            {
                vm.CloseHelpCommand.Execute(null);
                e.Handled = true;
            }
            else if (vm.IsQuestLogOpen)
            {
                vm.CloseQuestLogCommand.Execute(null);
                e.Handled = true;
            }
            else if (vm.IsBestiaryOpen)
            {
                vm.CloseBestiaryCommand.Execute(null);
                e.Handled = true;
            }
            else if (vm.IsAchievementsOpen)
            {
                vm.CloseAchievementsCommand.Execute(null);
                e.Handled = true;
            }
            else if (vm.IsSetCodexOpen)
            {
                vm.CloseSetCodexCommand.Execute(null);
                e.Handled = true;
            }
            else if (vm.Town is { IsQuestOfferOpen: true } offer)
            {
                offer.DeclineQuestOfferCommand.Execute(null);
                e.Handled = true;
            }
            else if (vm.IsSlotPanelOpen)
            {
                vm.CloseSlotsCommand.Execute(null);
                e.Handled = true;
            }
            else if (vm.Town is { IsInBuilding: true } inBuilding)
            {
                inBuilding.LeaveCommand.Execute(null);
                e.Handled = true;
            }
            else if (vm.Town is { IsSpellMenuOpen: true } spellMenu)
            {
                spellMenu.CloseSpellMenuCommand.Execute(null);
                e.Handled = true;
            }
            return;
        }

        // Enter / Return uses whatever you're standing on: a town building entrance,
        // or a dungeon stairway (out to town, up a level, or down a level).
        if (e.Key is Key.Enter or Key.Return)
        {
            if (vm.Town is { } town && town.EnterCommand.CanExecute(null))
            {
                town.EnterCommand.Execute(null);
                e.Handled = true;
            }
            else if (vm.Exploration is { IsInCombat: false } ex)
            {
                if (ex.CanReturnToTown) { ex.ReturnToTownCommand.Execute(null); e.Handled = true; }
                else if (ex.CanAscend) { ex.AscendCommand.Execute(null); e.Handled = true; }
                else if (ex.CanDescend) { ex.DescendCommand.Execute(null); e.Handled = true; }
            }
            return;
        }

        // J opens (and closes) the quest journal from anywhere — town or dungeon.
        if (e.Key is Key.J && !vm.IsSlotPanelOpen && !vm.IsSettingsOpen
            && vm.Town is not { IsQuestOfferOpen: true })
        {
            vm.ToggleQuestLog();
            e.Handled = true;
            return;
        }

        // B opens (and closes) the bestiary from anywhere.
        if (e.Key is Key.B && !vm.IsSlotPanelOpen && !vm.IsSettingsOpen
            && vm.Town is not { IsQuestOfferOpen: true })
        {
            vm.ToggleBestiary();
            e.Handled = true;
            return;
        }

        // K opens (and closes) the accessory-set codex from anywhere.
        if (e.Key is Key.K && !vm.IsSlotPanelOpen && !vm.IsSettingsOpen
            && vm.Town is not { IsQuestOfferOpen: true })
        {
            vm.ToggleSetCodex();
            e.Handled = true;
            return;
        }

        // Q assembles a ready-made party while in the Adventurers Guild.
        if (e.Key is Key.Q && vm.Town is { IsGuild: true } guild
            && guild.QuickPartyCommand.CanExecute(null))
        {
            guild.QuickPartyCommand.Execute(null);
            e.Handled = true;
            return;
        }

        IMover? mover = vm switch
        {
            { Exploration: { IsInCombat: false } exploration } => new ExplorationMover(exploration),
            { Town: { CanExplore: true } town } => new TownMover(town),
            _ => null
        };
        if (mover is null) return;

        switch (Resolve(e.Key))
        {
            case MoveAction.Forward: mover.Forward(); e.Handled = true; break;
            case MoveAction.Backward: mover.Backward(); e.Handled = true; break;
            case MoveAction.Left: mover.Left(); e.Handled = true; break;
            case MoveAction.Right: mover.Right(); e.Handled = true; break;
        }
    }

    public enum MoveAction { None, Forward, Backward, Left, Right }

    /// <summary>
    /// Maps a key to a movement action using the player's rebindable keys; the arrow keys are
    /// always honoured as a fixed fallback so movement can never become unreachable.
    /// </summary>
    public static MoveAction Resolve(Key key)
    {
        var s = AppSettings.Current;
        if (key == Key.Up || key == s.MoveForwardKey) return MoveAction.Forward;
        if (key == Key.Down || key == s.MoveBackwardKey) return MoveAction.Backward;
        if (key == Key.Left || key == s.TurnLeftKey) return MoveAction.Left;
        if (key == Key.Right || key == s.TurnRightKey) return MoveAction.Right;
        return MoveAction.None;
    }

    private interface IMover { void Forward(); void Backward(); void Left(); void Right(); }

    private sealed record ExplorationMover(ExplorationViewModel Vm) : IMover
    {
        public void Forward() => Vm.MoveForwardCommand.Execute(null);
        public void Backward() => Vm.MoveBackwardCommand.Execute(null);
        public void Left() => Vm.TurnLeftCommand.Execute(null);
        public void Right() => Vm.TurnRightCommand.Execute(null);
    }

    private sealed record TownMover(TownViewModel Vm) : IMover
    {
        public void Forward() => Vm.MoveForwardCommand.Execute(null);
        public void Backward() => Vm.MoveBackwardCommand.Execute(null);
        public void Left() => Vm.TurnLeftCommand.Execute(null);
        public void Right() => Vm.TurnRightCommand.Execute(null);
    }
}
