using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BardsTale.UI.Views;

public partial class ExplorationView : UserControl
{
    public ExplorationView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
