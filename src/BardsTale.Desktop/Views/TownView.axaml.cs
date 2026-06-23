using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BardsTale.Desktop.Views;

public partial class TownView : UserControl
{
    public TownView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
