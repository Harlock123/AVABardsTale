using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using BardsTale.UI.Services;
using BardsTale.UI.ViewModels;
using BardsTale.UI.Views;

namespace BardsTale.UI;

public partial class App : Application
{
    /// <summary>
    /// How the app obtains its save store. Defaults to the file-backed desktop store;
    /// the browser head overrides this with an IndexedDB-backed store before startup.
    /// </summary>
    public static System.Func<ISaveStore> SaveStoreFactory { get; set; } = () => new SaveService();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
        // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
        DisableAvaloniaDataAnnotationValidation();

        // On touch devices, enlarge tap targets (desktop/browser stay compact).
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
            Styles.Add(new StyleInclude(new Uri("avares://BardsTale.UI/"))
            {
                Source = new Uri("avares://BardsTale.UI/Styles/TouchStyles.axaml")
            });

        var viewModel = new MainWindowViewModel();
        switch (ApplicationLifetime)
        {
            // Desktop heads (Windows / macOS / Linux) host a top-level Window.
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = new MainWindow { DataContext = viewModel };
                break;
            // Single-view heads (Browser / mobile) host a root control instead of a Window.
            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = new MainView { DataContext = viewModel };
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}