using Avalonia.Input;
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
        IMover? mover = vm switch
        {
            { Exploration: { IsInCombat: false } exploration } => new ExplorationMover(exploration),
            { Town: { CanExplore: true } town } => new TownMover(town),
            _ => null
        };
        if (mover is null) return;

        switch (e.Key)
        {
            case Key.Up or Key.W: mover.Forward(); e.Handled = true; break;
            case Key.Down or Key.S: mover.Backward(); e.Handled = true; break;
            case Key.Left or Key.A: mover.Left(); e.Handled = true; break;
            case Key.Right or Key.D: mover.Right(); e.Handled = true; break;
        }
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
