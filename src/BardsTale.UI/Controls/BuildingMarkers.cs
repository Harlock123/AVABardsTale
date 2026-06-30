using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using BardsTale.Core.Town;

namespace BardsTale.UI.Controls;

/// <summary>Colour, glyph and label for one town building type.</summary>
public sealed record BuildingMarker(TownBuilding Building, string Label, string Glyph, IBrush Background, IBrush Foreground);

/// <summary>
/// Single source of truth for how each town building appears — shared by the
/// <see cref="MiniMap"/> markers and the on-screen legend so they never drift apart.
/// </summary>
public static class BuildingMarkers
{
    private static IBrush Rgb(byte r, byte g, byte b) => new SolidColorBrush(Color.FromRgb(r, g, b));

    public static IReadOnlyList<BuildingMarker> All { get; } = new[]
    {
        new BuildingMarker(TownBuilding.Guild,           "Guild",     "G", Rgb(0x4F, 0x7F, 0xC8), Brushes.White),
        new BuildingMarker(TownBuilding.Tavern,          "Tavern",    "T", Rgb(0xC9, 0x95, 0x2F), Rgb(0x1A, 0x16, 0x26)),
        new BuildingMarker(TownBuilding.ReviewBoard,     "Review",    "R", Rgb(0x8E, 0x6F, 0xC9), Brushes.White),
        new BuildingMarker(TownBuilding.Shop,            "Shop",      "$", Rgb(0xD8, 0x84, 0x2A), Brushes.White),
        new BuildingMarker(TownBuilding.Temple,          "Temple",    "+", Rgb(0xE6, 0xEC, 0xF2), Rgb(0xC0, 0x39, 0x2B)),
        new BuildingMarker(TownBuilding.Inn,             "Inn",       "I", Rgb(0x3F, 0xA4, 0x68), Brushes.White),
        new BuildingMarker(TownBuilding.QuestBoard,      "Quests",    "!", Rgb(0x2E, 0xA0, 0x9E), Brushes.White),
        new BuildingMarker(TownBuilding.Smithy,          "Smithy",    "⚒", Rgb(0x9C, 0x4A, 0x30), Brushes.White),
        new BuildingMarker(TownBuilding.Bank,            "Bank",      "B", Rgb(0xC9, 0xA8, 0x3A), Rgb(0x1A, 0x16, 0x26)),
        new BuildingMarker(TownBuilding.DungeonEntrance, "Catacombs", "▼", Rgb(0x9E, 0x2C, 0x2C), Brushes.White),
        new BuildingMarker(TownBuilding.TowerEntrance,   "Tower",     "♜", Rgb(0x6E, 0x57, 0x9C), Brushes.White),
    };

    private static readonly Dictionary<TownBuilding, BuildingMarker> ByType = All.ToDictionary(m => m.Building);

    private static readonly BuildingMarker Fallback =
        new(TownBuilding.None, "", "•", new SolidColorBrush(Color.FromRgb(0x55, 0x5A, 0x6E)), Brushes.White);

    /// <summary>The marker for a building — a neutral dot for any not in the table, so a new building can't crash the map.</summary>
    public static BuildingMarker For(TownBuilding building) => ByType.GetValueOrDefault(building, Fallback);
}
