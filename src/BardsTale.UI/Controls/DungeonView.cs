using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Geometry;

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

    static DungeonView()
    {
        AffectsRender<DungeonView>(MazeProperty, PartyXProperty, PartyYProperty, FacingProperty,
            RevisionProperty, HasLightProperty);
    }

    public Maze? Maze { get => GetValue(MazeProperty); set => SetValue(MazeProperty, value); }
    public int PartyX { get => GetValue(PartyXProperty); set => SetValue(PartyXProperty, value); }
    public int PartyY { get => GetValue(PartyYProperty); set => SetValue(PartyYProperty, value); }
    public Direction Facing { get => GetValue(FacingProperty); set => SetValue(FacingProperty, value); }
    public int Revision { get => GetValue(RevisionProperty); set => SetValue(RevisionProperty, value); }
    public bool HasLight { get => GetValue(HasLightProperty); set => SetValue(HasLightProperty, value); }

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
                DrawFrontWall(context, cx, cy, halfW[d + 1], halfH[d + 1], DepthShade(d + 1), cell.Feature);
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
        double halfW, double halfH, byte shade, CellFeature feature = CellFeature.None)
    {
        var rect = new Rect(cx - halfW, cy - halfH, halfW * 2, halfH * 2);
        ctx.DrawRectangle(new SolidColorBrush(Color.FromRgb(shade, shade, (byte)(shade * 0.92))),
            new Pen(Brushes.Black, 1.5), rect);

        DrawFeature(ctx, rect, feature);
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
