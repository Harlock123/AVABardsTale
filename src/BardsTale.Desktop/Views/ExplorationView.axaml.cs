using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BardsTale.Desktop.Views;

public partial class ExplorationView : UserControl
{
    public ExplorationView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
