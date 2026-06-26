using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using BardsTale.UI.ViewModels;

namespace BardsTale.UI.Views;

public partial class ExplorationView : UserControl
{
    private ExplorationViewModel? _vm;

    public ExplorationView()
    {
        InitializeComponent();
        Focusable = true; // so keyboard movement resumes once a riddle prompt closes
        DataContextChanged += (_, _) => HookViewModel();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void HookViewModel()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnViewModelPropertyChanged;
        _vm = DataContext as ExplorationViewModel;
        if (_vm is null) return;
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        if (_vm.IsAtRiddle) FocusAnswerBox(); // already mid-riddle (e.g. a loaded save)
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ExplorationViewModel.IsAtRiddle)) return;
        if (_vm!.IsAtRiddle) FocusAnswerBox();
        else Focus(); // reclaim focus so WASD/arrow movement works again
    }

    // Focus the answer box so the soft keyboard pops up on mobile (and typing works
    // immediately on desktop). Deferred so the box is realized and visible first.
    private void FocusAnswerBox() => Dispatcher.UIThread.Post(() =>
    {
        if (this.FindControl<TextBox>("RiddleAnswerBox") is { } box)
        {
            box.Focus();
            box.SelectAll();
        }
    }, DispatcherPriority.Loaded);

    /// <summary>Submits the riddle answer on Enter / the soft keyboard's Return key.</summary>
    private void OnRiddleAnswerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return)) return;
        if (DataContext is ExplorationViewModel vm && vm.AnswerRiddleCommand.CanExecute(null))
        {
            vm.AnswerRiddleCommand.Execute(null);
            e.Handled = true;
        }
    }
}
