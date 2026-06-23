using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BardsTale.Desktop.Views;

public partial class CombatView : UserControl
{
    public CombatView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
