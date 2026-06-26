using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using BardsTale.UI.ViewModels;

namespace BardsTale.UI.Views;

public partial class TownView : UserControl
{
    private TownViewModel? _vm;

    public TownView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => HookViewModel();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void HookViewModel()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnViewModelPropertyChanged;
        _vm = DataContext as TownViewModel;
        if (_vm is null) return;
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        if (_vm.IsGuild) FocusNameBox(); // already standing in the guild (e.g. a loaded save)
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Entering the Adventurers Guild should pop the soft keyboard ready to name a hero.
        if (e.PropertyName == nameof(TownViewModel.IsGuild) && _vm!.IsGuild)
            FocusNameBox();
    }

    // Focus the name box so the soft keyboard appears on mobile (and typing works
    // immediately on desktop). Deferred so the box is realized and visible first.
    private void FocusNameBox() => Dispatcher.UIThread.Post(() =>
    {
        if (this.FindControl<TextBox>("HeroNameBox") is { } box)
        {
            box.Focus();
            box.SelectAll();
        }
    }, DispatcherPriority.Loaded);

    /// <summary>
    /// Enter / the soft keyboard's Return key advances the creation flow without reaching
    /// for the mouse: roll a fresh recruit, or — if one is already rolled — recruit them.
    /// </summary>
    private void OnHeroNameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return)) return;
        if (DataContext is not TownViewModel vm) return;

        if (vm.Creation.HasCandidate && vm.RecruitCommand.CanExecute(null))
            vm.RecruitCommand.Execute(null);
        else if (vm.Creation.RollCommand.CanExecute(null))
            vm.Creation.RollCommand.Execute(null);
        else
            return;

        e.Handled = true;
    }
}
