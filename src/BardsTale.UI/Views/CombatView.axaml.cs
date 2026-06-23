using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BardsTale.UI.Views;

public partial class CombatView : UserControl
{
    public CombatView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
