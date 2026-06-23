namespace BardsTale.Core.Geometry;

/// <summary>
/// The four cardinal facings used for first-person grid movement.
/// </summary>
public enum Direction
{
    North = 0,
    East = 1,
    South = 2,
    West = 3
}

public static class DirectionExtensions
{
    public static Direction TurnRight(this Direction d) => (Direction)(((int)d + 1) & 3);

    public static Direction TurnLeft(this Direction d) => (Direction)(((int)d + 3) & 3);

    public static Direction Opposite(this Direction d) => (Direction)(((int)d + 2) & 3);

    /// <summary>Unit step in maze coordinates. North decreases Y (toward the top of the map).</summary>
    public static (int dx, int dy) Delta(this Direction d) => d switch
    {
        Direction.North => (0, -1),
        Direction.East => (1, 0),
        Direction.South => (0, 1),
        Direction.West => (-1, 0),
        _ => (0, 0)
    };

    public static string ToCompass(this Direction d) => d switch
    {
        Direction.North => "N",
        Direction.East => "E",
        Direction.South => "S",
        Direction.West => "W",
        _ => "?"
    };
}
