using CommunityToolkit.Mvvm.ComponentModel;

namespace BardsTale.UI.Settings;

/// <summary>
/// App-wide user preferences. A single shared instance (<see cref="Current"/>) is bound
/// by the settings screen and read live by the rest of the app (e.g. the sign animation).
/// </summary>
public sealed partial class AppSettings : ObservableObject
{
    public static AppSettings Current { get; } = new();

    /// <summary>Disables idle animations such as the swaying storefront signs.</summary>
    [ObservableProperty] private bool _reducedMotion;

    /// <summary>Whether the game saves automatically on returning to town.</summary>
    [ObservableProperty] private bool _autosave = true;

    /// <summary>Overall interface scale (0.8–1.2). 1.0 is the default size.</summary>
    [ObservableProperty] private double _uiScale = 1.0;

    /// <summary>Sound-effect volume (0–1).</summary>
    [ObservableProperty] private double _soundVolume = 0.7;

    /// <summary>Silences all sound effects and music.</summary>
    [ObservableProperty] private bool _muted;

    /// <summary>Whether looping background music plays.</summary>
    [ObservableProperty] private bool _musicEnabled = true;

    /// <summary>Background-music volume (0–1).</summary>
    [ObservableProperty] private double _musicVolume = 0.20;

    /// <summary>Whether music tracks crossfade into one another on a scene change (else a clean cut).</summary>
    [ObservableProperty] private bool _crossfadeMusic = true;
}
