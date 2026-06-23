using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BardsTale.UI.Views;

public partial class TownView : UserControl
{
    public TownView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
