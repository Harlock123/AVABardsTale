using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Geometry;
using BardsTale.Core.Town;

namespace BardsTale.UI.Controls;

/// <summary>
/// Renders a pseudo-3D first-person view of the maze, looking out from the
/// party's cell along its facing. Walls recede toward a central vanishing point.
/// </summary>
public sealed class DungeonView : Control
{
    private const int MaxDepth = 5;

    public static readonly StyledProperty<Maze?> MazeProperty =
        AvaloniaProperty.Register<DungeonView, Maze?>(nameof(Maze));

    public static readonly StyledProperty<int> PartyXProperty =
        AvaloniaProperty.Register<DungeonView, int>(nameof(PartyX));

    public static readonly StyledProperty<int> PartyYProperty =
        AvaloniaProperty.Register<DungeonView, int>(nameof(PartyY));

    public static readonly StyledProperty<Direction> FacingProperty =
        AvaloniaProperty.Register<DungeonView, Direction>(nameof(Facing));

    /// <summary>Bumped by the view model on every move so the control repaints.</summary>
    public static readonly StyledProperty<int> RevisionProperty =
        AvaloniaProperty.Register<DungeonView, int>(nameof(Revision));

    /// <summary>When true, conjured light reveals darkness cells instead of blacking them out.</summary>
    public static readonly StyledProperty<bool> HasLightProperty =
        AvaloniaProperty.Register<DungeonView, bool>(nameof(HasLight));

    /// <summary>Town building types by cell, used to put a stylized storefront sign on the facade.</summary>
    public static readonly StyledProperty<IReadOnlyDictionary<Position, TownBuilding>?> BuildingsProperty =
        AvaloniaProperty.Register<DungeonView, IReadOnlyDictionary<Position, TownBuilding>?>(nameof(Buildings));

    static DungeonView()
    {
        AffectsRender<DungeonView>(MazeProperty, PartyXProperty, PartyYProperty, FacingProperty,
            RevisionProperty, HasLightProperty, BuildingsProperty);
    }

    public Maze? Maze { get => GetValue(MazeProperty); set => SetValue(MazeProperty, value); }
    public int PartyX { get => GetValue(PartyXProperty); set => SetValue(PartyXProperty, value); }
    public int PartyY { get => GetValue(PartyYProperty); set => SetValue(PartyYProperty, value); }
    public Direction Facing { get => GetValue(FacingProperty); set => SetValue(FacingProperty, value); }
    public int Revision { get => GetValue(RevisionProperty); set => SetValue(RevisionProperty, value); }
    public bool HasLight { get => GetValue(HasLightProperty); set => SetValue(HasLightProperty, value); }
    public IReadOnlyDictionary<Position, TownBuilding>? Buildings
    {
        get => GetValue(BuildingsProperty);
        set => SetValue(BuildingsProperty, value);
    }

    private static readonly Color SignGold = Color.FromRgb(0xE8, 0xC5, 0x6B);

    private static readonly IBrush CeilingBrush = new SolidColorBrush(Color.FromRgb(28, 30, 40));
    private static readonly IBrush FloorBrush = new SolidColorBrush(Color.FromRgb(46, 40, 34));

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // Sky/ground split.
        context.FillRectangle(CeilingBrush, new Rect(0, 0, bounds.Width, bounds.Height / 2));
        context.FillRectangle(FloorBrush, new Rect(0, bounds.Height / 2, bounds.Width, bounds.Height / 2));

        var maze = Maze;
        if (maze is null) return;

        // In a darkness zone the party is blind — black out the whole view, unless they carry light.
        var here = new Position(PartyX, PartyY);
        if (!HasLight && maze.InBounds(here) && maze[here].Feature == CellFeature.Darkness)
        {
            context.FillRectangle(Brushes.Black, bounds);
            return;
        }

        var cx = bounds.Width / 2;
        var cy = bounds.Height / 2;

        // Precompute the opening rectangle at each depth boundary.
        var halfW = new double[MaxDepth + 2];
        var halfH = new double[MaxDepth + 2];
        for (var d = 0; d <= MaxDepth + 1; d++)
        {
            var s = Math.Pow(0.56, d);
            halfW[d] = cx * s;
            halfH[d] = cy * s;
        }

        var pos = new Position(PartyX, PartyY);
        var forward = Facing;
        var left = Facing.TurnLeft();
        var right = Facing.TurnRight();

        for (var d = 0; d <= MaxDepth; d++)
        {
            if (!maze.InBounds(pos))
            {
                DrawFrontWall(context, cx, cy, halfW[d], halfH[d], DepthShade(d));
                break;
            }

            var cell = maze[pos];

            if (cell.HasWall(left))
                DrawSideWall(context, cx, cy, halfW[d], halfH[d], halfW[d + 1], halfH[d + 1], isLeft: true, DepthShade(d));
            if (cell.HasWall(right))
                DrawSideWall(context, cx, cy, halfW[d], halfH[d], halfW[d + 1], halfH[d + 1], isLeft: false, DepthShade(d));

            var blockedAhead = cell.HasWall(forward) || !maze.InBounds(pos.Step(forward));
            if (blockedAhead)
            {
                // A building facade gets its name on a stylized sign, tinted by its type.
                string? signName = null;
                var accent = SignGold;
                if (cell.Feature == CellFeature.Building && !string.IsNullOrEmpty(cell.Text))
                {
                    signName = cell.Text;
                    if (Buildings is { } b && b.TryGetValue(pos, out var type)
                        && BuildingMarkers.For(type).Background is ISolidColorBrush accentBrush)
                        accent = accentBrush.Color;
                }
                DrawFrontWall(context, cx, cy, halfW[d + 1], halfH[d + 1], DepthShade(d + 1), cell.Feature, signName, accent);
                break;
            }

            pos = pos.Step(forward);
        }
    }

    private static byte DepthShade(int depth)
    {
        // Nearer walls are brighter; fades into the dark with distance.
        var v = 150 - depth * 24;
        return (byte)Math.Clamp(v, 30, 255);
    }

    private static void DrawSideWall(DrawingContext ctx, double cx, double cy,
        double nearHalfW, double nearHalfH, double farHalfW, double farHalfH, bool isLeft, byte shade)
    {
        var sign = isLeft ? -1 : 1;
        var nx = cx + sign * nearHalfW;
        var fx = cx + sign * farHalfW;

        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new Point(nx, cy - nearHalfH), true);
            g.LineTo(new Point(fx, cy - farHalfH));
            g.LineTo(new Point(fx, cy + farHalfH));
            g.LineTo(new Point(nx, cy + nearHalfH));
            g.EndFigure(true);
        }

        // Side walls a touch darker than front walls for shape readability.
        var s = (byte)(shade * 0.8);
        ctx.DrawGeometry(new SolidColorBrush(Color.FromRgb(s, s, (byte)(s * 0.9))),
            new Pen(Brushes.Black, 1), geo);
    }

    private static void DrawFrontWall(DrawingContext ctx, double cx, double cy,
        double halfW, double halfH, byte shade, CellFeature feature = CellFeature.None,
        string? signName = null, Color accent = default)
    {
        var rect = new Rect(cx - halfW, cy - halfH, halfW * 2, halfH * 2);
        ctx.DrawRectangle(new SolidColorBrush(Color.FromRgb(shade, shade, (byte)(shade * 0.92))),
            new Pen(Brushes.Black, 1.5), rect);

        DrawFeature(ctx, rect, feature);

        if (!string.IsNullOrEmpty(signName))
            DrawStorefront(ctx, rect, signName, accent);
    }

    // A hanging storefront sign on the building facade: a weathered wood board that
    // hangs from an iron bracket, with a coloured frame (matching the map marker) and
    // the building's name in gold serif lettering.
    private static void DrawStorefront(DrawingContext ctx, Rect wall, string name, Color accent)
    {
        var signW = wall.Width * 0.84;
        var signH = wall.Height * 0.17;
        if (signW < 16 || signH < 9) return; // too small/far to be legible

        var board = new Rect(wall.X + (wall.Width - signW) / 2, wall.Y + wall.Height * 0.10, signW, signH);
        var radius = signH * 0.18;

        // --- hanging hardware: a wall-mounted iron bracket with two chains ---
        var iron = new SolidColorBrush(Color.FromRgb(0x55, 0x5A, 0x69));
        var ironDark = new SolidColorBrush(Color.FromRgb(0x23, 0x26, 0x30));
        var beamH = System.Math.Max(2, wall.Height * 0.022);
        var beam = new Rect(board.X - signW * 0.05, wall.Y + wall.Height * 0.035, signW * 1.10, beamH);
        ctx.DrawRectangle(iron, new Pen(ironDark, 1), beam, beamH * 0.4, beamH * 0.4);

        var chainW = System.Math.Max(1.5, signW * 0.012);
        var chainPen = new Pen(iron, chainW, lineCap: PenLineCap.Round);
        var bolt = chainW * 1.3;
        foreach (var fx in new[] { 0.15, 0.85 })
        {
            var x = board.X + board.Width * fx;
            ctx.DrawLine(chainPen, new Point(x, beam.Bottom), new Point(x, board.Y + 1));
            ctx.DrawEllipse(ironDark, new Pen(iron, 1), new Point(x, beam.Bottom), bolt, bolt);
            ctx.DrawEllipse(ironDark, new Pen(iron, 1), new Point(x, board.Y + 1), bolt, bolt);
        }

        // --- weathered wood board ---
        ctx.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x2E, 0x21, 0x14)), null, board, radius, radius);
        using (ctx.PushClip(board))
        {
            // horizontal grain
            var grain = new Pen(new SolidColorBrush(Color.FromArgb(60, 0x52, 0x3D, 0x24)),
                System.Math.Max(0.6, signH * 0.03));
            foreach (var gy in new[] { 0.2, 0.38, 0.55, 0.72, 0.87 })
                ctx.DrawLine(grain, new Point(board.X, board.Y + board.Height * gy),
                    new Point(board.Right, board.Y + board.Height * gy));
            // vertical plank seams
            var seam = new Pen(new SolidColorBrush(Color.FromArgb(80, 0x10, 0x0A, 0x05)),
                System.Math.Max(0.6, signW * 0.005));
            foreach (var sx in new[] { 0.34, 0.67 })
                ctx.DrawLine(seam, new Point(board.X + board.Width * sx, board.Y),
                    new Point(board.X + board.Width * sx, board.Bottom));
            // knots / wear spots
            var knot = new SolidColorBrush(Color.FromArgb(65, 0x14, 0x0D, 0x06));
            ctx.DrawEllipse(knot, null, new Point(board.X + board.Width * 0.2, board.Y + board.Height * 0.62),
                signH * 0.09, signH * 0.07);
            ctx.DrawEllipse(knot, null, new Point(board.X + board.Width * 0.79, board.Y + board.Height * 0.33),
                signH * 0.08, signH * 0.06);
        }
        // aged inner shadow + coloured frame
        ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(85, 0, 0, 0)),
            System.Math.Max(1, signH * 0.05)), board.Deflate(signH * 0.05), radius, radius);
        ctx.DrawRectangle(null, new Pen(new SolidColorBrush(accent), System.Math.Max(1, signH * 0.08)),
            board, radius, radius);

        // --- name in gold serif ---
        var typeface = new Typeface(new FontFamily("Georgia, Times New Roman, serif"),
            FontStyle.Normal, FontWeight.Bold);
        var fontSize = signH * 0.58;
        var text = new FormattedText(name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            typeface, fontSize, new SolidColorBrush(SignGold));
        var maxW = board.Width * 0.88;
        if (text.Width > maxW)
        {
            text = new FormattedText(name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                typeface, fontSize * maxW / text.Width, new SolidColorBrush(SignGold));
        }
        ctx.DrawText(text, new Point(board.Center.X - text.Width / 2, board.Center.Y - text.Height / 2));
    }

    private static void DrawFeature(DrawingContext ctx, Rect wall, CellFeature feature)
    {
        switch (feature)
        {
            case CellFeature.StairsDown:
            case CellFeature.StairsUp:
            {
                var pen = new Pen(Brushes.Black, 2);
                var steps = 4;
                var down = feature == CellFeature.StairsDown;
                for (var i = 0; i < steps; i++)
                {
                    var t = i / (double)steps;
                    var y = down ? wall.Y + wall.Height * (0.4 + t * 0.5)
                                 : wall.Bottom - wall.Height * (0.4 + t * 0.5);
                    var inset = wall.Width * 0.15 * t;
                    ctx.DrawLine(pen,
                        new Point(wall.X + inset + wall.Width * 0.2, y),
                        new Point(wall.Right - inset - wall.Width * 0.2, y));
                }
                break;
            }
            case CellFeature.Door:
            case CellFeature.Building:
            {
                var door = new Rect(wall.X + wall.Width * 0.3, wall.Y + wall.Height * 0.25,
                    wall.Width * 0.4, wall.Height * 0.7);
                ctx.DrawRectangle(new SolidColorBrush(Color.FromRgb(80, 50, 30)), new Pen(Brushes.Black, 2), door);
                break;
            }
        }
    }
}
