using BardsTale.Core.Geometry;

namespace BardsTale.Core.Dungeon;

[Flags]
public enum Walls
{
    None = 0,
    North = 1 << 0,
    East = 1 << 1,
    South = 1 << 2,
    West = 1 << 3
}

public enum CellFeature
{
    None,
    StairsUp,
    StairsDown,
    Door,
    Darkness,
    Trap,
    SpinnerTrap,
    Message,
    Exit,
    Building,
    Teleporter,
    AntiMagic,
    BossLair,
    /// <summary>A treasure chest — may be trapped; a Rogue can disarm it before it's opened.</summary>
    Chest
}

/// <summary>One tile of a maze level.</summary>
public sealed class Cell
{
    public Walls Walls { get; set; } = Walls.None;
    public CellFeature Feature { get; set; } = CellFeature.None;
    public string? Text { get; set; }
    public bool Visited { get; set; }

    /// <summary>For teleporters, the cell the party is whisked to.</summary>
    public Position? Destination { get; set; }

    public bool HasWall(Direction dir) => (Walls & ToWallFlag(dir)) != 0;

    public static Walls ToWallFlag(Direction dir) => dir switch
    {
        Direction.North => Walls.North,
        Direction.East => Walls.East,
        Direction.South => Walls.South,
        Direction.West => Walls.West,
        _ => Walls.None
    };
}

/// <summary>
/// A single dungeon level: a rectangular grid of cells. Coordinate (0,0) is the
/// top-left; North decreases Y.
/// </summary>
public sealed class Maze
{
    private readonly Cell[,] _cells;

    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public Position StartPosition { get; set; }
    public Direction StartFacing { get; set; } = Direction.North;

    public Maze(string name, int width, int height)
    {
        Name = name;
        Width = width;
        Height = height;
        _cells = new Cell[width, height];
        for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
                _cells[x, y] = new Cell();
    }

    public bool InBounds(Position p) => p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;

    public Cell this[Position p] => _cells[p.X, p.Y];
    public Cell this[int x, int y] => _cells[x, y];

    /// <summary>The first cell carrying the given feature, scanning row-major, or null if none.</summary>
    public Position? PositionOf(CellFeature feature)
    {
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                if (_cells[x, y].Feature == feature)
                    return new Position(x, y);
        return null;
    }

    /// <summary>True when the party can step from <paramref name="from"/> toward <paramref name="dir"/>.</summary>
    public bool CanMove(Position from, Direction dir)
    {
        if (!InBounds(from)) return false;
        if (this[from].HasWall(dir)) return false;
        var target = from.Step(dir);
        return InBounds(target);
    }

    /// <summary>Raise a wall between a cell and its neighbour, keeping both sides consistent.</summary>
    public void SetWall(int x, int y, Direction dir, bool present = true)
    {
        var flag = Cell.ToWallFlag(dir);
        var cell = _cells[x, y];
        cell.Walls = present ? cell.Walls | flag : cell.Walls & ~flag;

        var neighbour = new Position(x, y).Step(dir);
        if (!InBounds(neighbour)) return;
        var opp = Cell.ToWallFlag(dir.Opposite());
        var nCell = this[neighbour];
        nCell.Walls = present ? nCell.Walls | opp : nCell.Walls & ~opp;
    }

    /// <summary>Surround the maze with an outer boundary wall.</summary>
    public void SealBorders()
    {
        for (var x = 0; x < Width; x++)
        {
            _cells[x, 0].Walls |= Walls.North;
            _cells[x, Height - 1].Walls |= Walls.South;
        }
        for (var y = 0; y < Height; y++)
        {
            _cells[0, y].Walls |= Walls.West;
            _cells[Width - 1, y].Walls |= Walls.East;
        }
    }
}
