using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Geometry;
using BardsTale.Core.Town;

namespace BardsTale.UI.Controls;

/// <summary>Top-down auto-map. Only cells the party has visited are revealed.</summary>
public sealed class MiniMap : Control
{
    public static readonly StyledProperty<Maze?> MazeProperty =
        AvaloniaProperty.Register<MiniMap, Maze?>(nameof(Maze));

    public static readonly StyledProperty<int> PartyXProperty =
        AvaloniaProperty.Register<MiniMap, int>(nameof(PartyX));

    public static readonly StyledProperty<int> PartyYProperty =
        AvaloniaProperty.Register<MiniMap, int>(nameof(PartyY));

    public static readonly StyledProperty<Direction> FacingProperty =
        AvaloniaProperty.Register<MiniMap, Direction>(nameof(Facing));

    public static readonly StyledProperty<int> RevisionProperty =
        AvaloniaProperty.Register<MiniMap, int>(nameof(Revision));

    /// <summary>When true, the whole map is drawn (walls, doors, features) rather than
    /// only the cells the party has visited. Used for the known town overworld.</summary>
    public static readonly StyledProperty<bool> RevealAllProperty =
        AvaloniaProperty.Register<MiniMap, bool>(nameof(RevealAll));

    /// <summary>Maps cell positions to their town building type, so each is drawn with a
    /// distinct marker. Null for the dungeon, where building markers don't apply.</summary>
    public static readonly StyledProperty<IReadOnlyDictionary<Position, TownBuilding>?> BuildingsProperty =
        AvaloniaProperty.Register<MiniMap, IReadOnlyDictionary<Position, TownBuilding>?>(nameof(Buildings));

    static MiniMap()
    {
        AffectsRender<MiniMap>(MazeProperty, PartyXProperty, PartyYProperty, FacingProperty,
            RevisionProperty, RevealAllProperty, BuildingsProperty);
    }

    public Maze? Maze { get => GetValue(MazeProperty); set => SetValue(MazeProperty, value); }
    public int PartyX { get => GetValue(PartyXProperty); set => SetValue(PartyXProperty, value); }
    public int PartyY { get => GetValue(PartyYProperty); set => SetValue(PartyYProperty, value); }
    public Direction Facing { get => GetValue(FacingProperty); set => SetValue(FacingProperty, value); }
    public int Revision { get => GetValue(RevisionProperty); set => SetValue(RevisionProperty, value); }
    public bool RevealAll { get => GetValue(RevealAllProperty); set => SetValue(RevealAllProperty, value); }
    public IReadOnlyDictionary<Position, TownBuilding>? Buildings
    {
        get => GetValue(BuildingsProperty);
        set => SetValue(BuildingsProperty, value);
    }

    private static readonly IBrush UnknownBrush = new SolidColorBrush(Color.FromRgb(18, 18, 22));
    private static readonly IBrush KnownBrush = new SolidColorBrush(Color.FromRgb(60, 64, 78));
    private static readonly IBrush PartyBrush = Brushes.Gold;
    private static readonly Pen WallPen = new(new SolidColorBrush(Color.FromRgb(170, 175, 190)), 1.4);

    public override void Render(DrawingContext context)
    {
        var maze = Maze;
        if (maze is null) return;

        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0) return;

        var cell = size / Math.Max(maze.Width, maze.Height);
        var ox = (Bounds.Width - cell * maze.Width) / 2;
        var oy = (Bounds.Height - cell * maze.Height) / 2;

        for (var x = 0; x < maze.Width; x++)
        {
            for (var y = 0; y < maze.Height; y++)
            {
                var c = maze[x, y];
                var revealed = RevealAll || c.Visited;
                var rect = new Rect(ox + x * cell, oy + y * cell, cell, cell);
                context.FillRectangle(revealed ? KnownBrush : UnknownBrush, rect);
                if (!revealed) continue;

                if (c.HasWall(Direction.North))
                    context.DrawLine(WallPen, rect.TopLeft, rect.TopRight);
                if (c.HasWall(Direction.South))
                    context.DrawLine(WallPen, rect.BottomLeft, rect.BottomRight);
                if (c.HasWall(Direction.West))
                    context.DrawLine(WallPen, rect.TopLeft, rect.BottomLeft);
                if (c.HasWall(Direction.East))
                    context.DrawLine(WallPen, rect.TopRight, rect.BottomRight);

                if (Buildings is { } buildings && buildings.TryGetValue(new Position(x, y), out var building))
                    DrawBuildingMarker(context, rect, building);
                else
                    DrawFeatureMarker(context, rect, c.Feature);
            }
        }

        DrawParty(context, ox, oy, cell);
    }

    private static void DrawFeatureMarker(DrawingContext ctx, Rect rect, CellFeature feature)
    {
        IBrush? brush = feature switch
        {
            CellFeature.StairsDown => Brushes.OrangeRed,
            CellFeature.StairsUp => Brushes.MediumSeaGreen,
            CellFeature.Message => Brushes.DeepSkyBlue,
            CellFeature.Building => Brushes.Gold,
            CellFeature.Teleporter => Brushes.Violet,
            CellFeature.SpinnerTrap => Brushes.Turquoise,
            CellFeature.Trap => Brushes.Crimson,
            CellFeature.AntiMagic => Brushes.SlateGray,
            CellFeature.BossLair => Brushes.DarkRed,
            CellFeature.Riddle => Brushes.MediumPurple,
            _ => null
        };
        if (brush is null) return;
        var inset = rect.Width * 0.3;
        ctx.FillRectangle(brush, rect.Deflate(inset));
    }

    private static void DrawBuildingMarker(DrawingContext ctx, Rect rect, TownBuilding building)
    {
        var style = BuildingMarkers.For(building);
        var marker = rect.Deflate(rect.Width * 0.16);
        ctx.DrawRectangle(style.Background, null, marker, marker.Width * 0.22, marker.Height * 0.22);

        if (building == TownBuilding.DungeonEntrance)
        {
            // A downward triangle = the stair leading down into the catacombs.
            var t = marker.Deflate(marker.Width * 0.24);
            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                g.BeginFigure(t.TopLeft, true);
                g.LineTo(t.TopRight);
                g.LineTo(new Point(t.Center.X, t.Bottom));
                g.EndFigure(true);
            }
            ctx.DrawGeometry(style.Foreground, null, geo);
            return;
        }

        var text = new FormattedText(style.Glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            Typeface.Default, rect.Height * 0.52, style.Foreground);
        ctx.DrawText(text, new Point(marker.Center.X - text.Width / 2, marker.Center.Y - text.Height / 2));
    }

    private void DrawParty(DrawingContext ctx, double ox, double oy, double cell)
    {
        var cxp = ox + PartyX * cell + cell / 2;
        var cyp = oy + PartyY * cell + cell / 2;
        var r = cell * 0.32;

        // Triangle pointing in the facing direction.
        var (dx, dy) = Facing.Delta();
        var tip = new Point(cxp + dx * r, cyp + dy * r);
        var backCx = cxp - dx * r * 0.6;
        var backCy = cyp - dy * r * 0.6;
        var perpX = -dy * r * 0.7;
        var perpY = dx * r * 0.7;

        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(tip, true);
            g.LineTo(new Point(backCx + perpX, backCy + perpY));
            g.LineTo(new Point(backCx - perpX, backCy - perpY));
            g.EndFigure(true);
        }
        ctx.DrawGeometry(PartyBrush, new Pen(Brushes.Black, 1), geo);
    }
}
