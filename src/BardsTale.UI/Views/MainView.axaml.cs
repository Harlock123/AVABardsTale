using Avalonia.Controls;
using Avalonia.Input;
using BardsTale.UI.ViewModels;

namespace BardsTale.UI.Views;

/// <summary>
/// The root game view, hosted directly by single-view heads (browser/mobile) and
/// wrapped in a <see cref="MainWindow"/> by the desktop head. Holds all the shell
/// chrome (top bar, victory/credits overlay, save-slot picker).
/// </summary>
public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (DataContext is MainWindowViewModel vm)
            MovementKeys.Handle(vm, e);
    }
}
