using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using BardsTale.UI.ViewModels;

namespace BardsTale.UI.Views;

/// <summary>
/// The root game view, hosted directly by single-view heads (browser/mobile) and
/// wrapped in a <see cref="MainWindow"/> by the desktop head. Holds all the shell
/// chrome (top bar, victory/credits overlay, save-slot picker) and routes the
/// WASD / arrow / Enter keys to movement and building entry.
/// </summary>
public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        // Tunnel so navigation keys reach us before a focused on-screen button can
        // swallow them (e.g. Enter activating a button instead of entering a building).
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        // A tap on non-interactive space reclaims keyboard focus (matters on mobile).
        AddHandler(PointerPressedEvent, OnPreviewPointerPressed, RoutingStrategies.Tunnel);
    }

    // Grab keyboard focus as soon as we're shown so WASD/Enter work without an
    // initial click. The deferred pass is what makes focus reliably stick on mobile
    // (Android), where focusing at attach-time alone is too early.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Focus();
        Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Loaded);
    }

    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // If the tap landed on a focusable control (button, text box, combo), let it
        // take focus; otherwise pull focus back to the root so keys keep working.
        if (e.Source is InputElement { Focusable: true } src && !ReferenceEquals(src, this)) return;
        Focus();
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        // Never hijack typing (e.g. entering a hero's name in the Guild) — but Escape
        // should still back out of a building even while a text box has focus.
        if (e.Source is TextBox && e.Key != Key.Escape) return;
        if (DataContext is not MainWindowViewModel vm) return;

        MovementKeys.Handle(vm, e);

        // Closing a panel/building removes the control that had focus, so reclaim it
        // here to keep WASD/Enter working without another click.
        if (e.Handled && e.Key == Key.Escape) Focus();
    }
}
