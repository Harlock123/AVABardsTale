using Avalonia.Controls;
using Avalonia.Input;
using BardsTale.Desktop.ViewModels;

namespace BardsTale.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>Maps arrow keys / WASD to movement while exploring the dungeon or the town.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (DataContext is not MainWindowViewModel vm) return;

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

    private sealed record ExplorationMover(ViewModels.ExplorationViewModel Vm) : IMover
    {
        public void Forward() => Vm.MoveForwardCommand.Execute(null);
        public void Backward() => Vm.MoveBackwardCommand.Execute(null);
        public void Left() => Vm.TurnLeftCommand.Execute(null);
        public void Right() => Vm.TurnRightCommand.Execute(null);
    }

    private sealed record TownMover(ViewModels.TownViewModel Vm) : IMover
    {
        public void Forward() => Vm.MoveForwardCommand.Execute(null);
        public void Backward() => Vm.MoveBackwardCommand.Execute(null);
        public void Left() => Vm.TurnLeftCommand.Execute(null);
        public void Right() => Vm.TurnRightCommand.Execute(null);
    }
}
