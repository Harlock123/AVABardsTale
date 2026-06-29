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
    /// <summary>A treasure chest — may be trapped (or a disguised mimic); a Rogue can disarm it.</summary>
    Chest,
    /// <summary>A gilded chest: always trapped and richer, with a guaranteed warding accessory.</summary>
    OrnateChest,
    /// <summary>An inscribed tile that poses a riddle — answer it for a reward.</summary>
    Riddle,
    /// <summary>A rune-etched lever — pulling it raises the barred gates sealing this level's vaults.</summary>
    Lever,
    /// <summary>An iron key lying on the floor — step onto it to pocket it for a locked door.</summary>
    Key,
    /// <summary>A non-combat dungeon event — a scene with choices (see <see cref="DungeonEvents"/>).</summary>
    Event,
    /// <summary>An altar that grants a dive-long party boon in exchange for a gold offering.</summary>
    Shrine
}

/// <summary>One tile of a maze level.</summary>
public sealed class Cell
{
    public Walls Walls { get; set; } = Walls.None;
    public CellFeature Feature { get; set; } = CellFeature.None;
    public string? Text { get; set; }
    public bool Visited { get; set; }

    /// <summary>Which present walls are actually hidden doors, openable by searching.</summary>
    public Walls SecretDoors { get; set; } = Walls.None;

    /// <summary>Which present walls are barred gates (visible portcullises) raised by a lever.</summary>
    public Walls Gates { get; set; } = Walls.None;

    /// <summary>Which present walls are locked doors, opened by spending a carried key.</summary>
    public Walls LockedDoors { get; set; } = Walls.None;

    /// <summary>Which present walls are illusions — they read as solid stone, but the party walks straight through.</summary>
    public Walls IllusoryWalls { get; set; } = Walls.None;

    /// <summary>Which walls are one-way exits: open heading out, but the neighbour stays walled, so there is no coming back.</summary>
    public Walls OneWayDoors { get; set; } = Walls.None;

    /// <summary>For riddle tiles, which riddle is inscribed (index into the riddle catalogue).</summary>
    public int RiddleId { get; set; } = -1;

    /// <summary>For event tiles, which dungeon event plays out (index into the event catalogue).</summary>
    public int EventId { get; set; } = -1;

    /// <summary>For teleporters, the cell the party is whisked to.</summary>
    public Position? Destination { get; set; }

    public bool HasWall(Direction dir) => (Walls & ToWallFlag(dir)) != 0;

    /// <summary>True when the wall toward <paramref name="dir"/> is a closed, barred gate.</summary>
    public bool HasGate(Direction dir) => (Gates & ToWallFlag(dir)) != 0;

    /// <summary>True when the wall toward <paramref name="dir"/> is a locked door.</summary>
    public bool HasLockedDoor(Direction dir) => (LockedDoors & ToWallFlag(dir)) != 0;

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
        var cell = this[from];
        var flag = Cell.ToWallFlag(dir);
        // A wall blocks — unless it is an illusion, which the party can pass straight through.
        if ((cell.Walls & flag) != 0 && (cell.IllusoryWalls & flag) == 0) return false;
        var target = from.Step(dir);
        return InBounds(target);
    }

    /// <summary>True when the wall toward <paramref name="dir"/> from <paramref name="p"/> is an illusion.</summary>
    public bool HasIllusoryWall(Position p, Direction dir) =>
        InBounds(p) && (this[p].IllusoryWalls & Cell.ToWallFlag(dir)) != 0;

    /// <summary>The directions from a cell whose "wall" is actually an undiscovered illusion.</summary>
    public IReadOnlyList<Direction> IllusoryWallsAt(Position p)
    {
        var found = new List<Direction>();
        if (!InBounds(p)) return found;
        var cell = this[p];
        foreach (var d in AllDirections)
            if ((cell.IllusoryWalls & Cell.ToWallFlag(d)) != 0)
                found.Add(d);
        return found;
    }

    private static readonly Direction[] AllDirections =
        { Direction.North, Direction.East, Direction.South, Direction.West };

    /// <summary>The directions from a cell that hold an as-yet-undiscovered secret door.</summary>
    public IReadOnlyList<Direction> SecretDoorsAt(Position p)
    {
        var found = new List<Direction>();
        if (!InBounds(p)) return found;
        var cell = this[p];
        foreach (var d in AllDirections)
            if ((cell.SecretDoors & Cell.ToWallFlag(d)) != 0)
                found.Add(d);
        return found;
    }

    /// <summary>Marks a wall as a hidden door (it reads as a solid wall until searched out).</summary>
    public void MarkSecretDoor(int x, int y, Direction dir)
    {
        SetWall(x, y, dir, present: true);
        this[x, y].SecretDoors |= Cell.ToWallFlag(dir);
        var n = new Position(x, y).Step(dir);
        if (InBounds(n)) this[n].SecretDoors |= Cell.ToWallFlag(dir.Opposite());
    }

    /// <summary>How many undiscovered secret doors remain on this level (each counted once).</summary>
    public int HiddenSecretCount()
    {
        var count = 0;
        for (var x = 0; x < Width; x++)
            for (var y = 0; y < Height; y++)
            {
                // Count only North/West flags so each shared door is tallied a single time.
                var s = _cells[x, y].SecretDoors;
                if ((s & Walls.North) != 0) count++;
                if ((s & Walls.West) != 0) count++;
            }
        return count;
    }

    /// <summary>How many cells on this level still carry the given feature.</summary>
    public int CountFeature(CellFeature feature)
    {
        var count = 0;
        for (var x = 0; x < Width; x++)
            for (var y = 0; y < Height; y++)
                if (_cells[x, y].Feature == feature) count++;
        return count;
    }

    /// <summary>Opens a discovered secret door — removes the wall and clears the flag on both sides.</summary>
    public void OpenSecretDoor(Position p, Direction dir)
    {
        SetWall(p.X, p.Y, dir, present: false);
        this[p].SecretDoors &= ~Cell.ToWallFlag(dir);
        var n = p.Step(dir);
        if (InBounds(n)) this[n].SecretDoors &= ~Cell.ToWallFlag(dir.Opposite());
    }

    /// <summary>Marks a wall as a barred gate — it blocks passage (and reads as a portcullis) until a lever raises it.</summary>
    public void MarkGate(int x, int y, Direction dir)
    {
        SetWall(x, y, dir, present: true);
        this[x, y].Gates |= Cell.ToWallFlag(dir);
        var n = new Position(x, y).Step(dir);
        if (InBounds(n)) this[n].Gates |= Cell.ToWallFlag(dir.Opposite());
    }

    /// <summary>How many barred gates remain closed on this level (each counted once).</summary>
    public int GateCount()
    {
        var count = 0;
        for (var x = 0; x < Width; x++)
            for (var y = 0; y < Height; y++)
            {
                // Count only North/West flags so each shared gate is tallied a single time.
                var g = _cells[x, y].Gates;
                if ((g & Walls.North) != 0) count++;
                if ((g & Walls.West) != 0) count++;
            }
        return count;
    }

    /// <summary>Raises every barred gate on the level (a lever's doing); returns how many opened.</summary>
    public int OpenAllGates()
    {
        var opened = GateCount();
        for (var x = 0; x < Width; x++)
            for (var y = 0; y < Height; y++)
            {
                var cell = _cells[x, y];
                if (cell.Gates == Walls.None) continue;
                foreach (var d in AllDirections)
                    if ((cell.Gates & Cell.ToWallFlag(d)) != 0)
                        SetWall(x, y, d, present: false);
                cell.Gates = Walls.None;
            }
        return opened;
    }

    /// <summary>Marks a wall as a locked door — it blocks passage until a carried key is spent on it.</summary>
    public void MarkLockedDoor(int x, int y, Direction dir)
    {
        SetWall(x, y, dir, present: true);
        this[x, y].LockedDoors |= Cell.ToWallFlag(dir);
        var n = new Position(x, y).Step(dir);
        if (InBounds(n)) this[n].LockedDoors |= Cell.ToWallFlag(dir.Opposite());
    }

    /// <summary>The directions from a cell that hold a still-locked door.</summary>
    public IReadOnlyList<Direction> LockedDoorsAt(Position p)
    {
        var found = new List<Direction>();
        if (!InBounds(p)) return found;
        var cell = this[p];
        foreach (var d in AllDirections)
            if ((cell.LockedDoors & Cell.ToWallFlag(d)) != 0)
                found.Add(d);
        return found;
    }

    /// <summary>Unlocks a door — removes the wall and clears the locked flag on both sides.</summary>
    public void OpenLockedDoor(Position p, Direction dir)
    {
        SetWall(p.X, p.Y, dir, present: false);
        this[p].LockedDoors &= ~Cell.ToWallFlag(dir);
        var n = p.Step(dir);
        if (InBounds(n)) this[n].LockedDoors &= ~Cell.ToWallFlag(dir.Opposite());
    }

    /// <summary>How many locked doors remain on this level (each counted once).</summary>
    public int LockedDoorCount()
    {
        var count = 0;
        for (var x = 0; x < Width; x++)
            for (var y = 0; y < Height; y++)
            {
                var l = _cells[x, y].LockedDoors;
                if ((l & Walls.North) != 0) count++;
                if ((l & Walls.West) != 0) count++;
            }
        return count;
    }

    /// <summary>Disguises an existing wall as an illusion: it still reads as solid stone (and renders as a
    /// wall), but the party can walk straight through it. Marked on both sides so it is passable either way.</summary>
    public void MarkIllusoryWall(int x, int y, Direction dir)
    {
        SetWall(x, y, dir, present: true);
        this[x, y].IllusoryWalls |= Cell.ToWallFlag(dir);
        var n = new Position(x, y).Step(dir);
        if (InBounds(n)) this[n].IllusoryWalls |= Cell.ToWallFlag(dir.Opposite());
    }

    /// <summary>Dispels a discovered illusory wall — clears the wall and the illusion flag on both sides,
    /// leaving an ordinary open passage.</summary>
    public void RevealIllusoryWall(Position p, Direction dir)
    {
        SetWall(p.X, p.Y, dir, present: false);
        this[p].IllusoryWalls &= ~Cell.ToWallFlag(dir);
        var n = p.Step(dir);
        if (InBounds(n)) this[n].IllusoryWalls &= ~Cell.ToWallFlag(dir.Opposite());
    }

    /// <summary>Opens a one-way passage: the wall toward <paramref name="dir"/> is removed so the party can
    /// step out, but the neighbour keeps its wall, so it cannot be re-entered from the far side.</summary>
    public void MarkOneWayDoor(int x, int y, Direction dir)
    {
        var flag = Cell.ToWallFlag(dir);
        var cell = _cells[x, y];
        cell.Walls &= ~flag;        // open the near (forward) side
        cell.OneWayDoors |= flag;   // remember it's a one-way exit, for narration
        var n = new Position(x, y).Step(dir);
        if (InBounds(n))
            _cells[n.X, n.Y].Walls |= Cell.ToWallFlag(dir.Opposite()); // the far side stays walled
    }

    /// <summary>How many one-way doors lead out of this level's cells (each counted once, on the open side).</summary>
    public int OneWayDoorCount()
    {
        var count = 0;
        for (var x = 0; x < Width; x++)
            for (var y = 0; y < Height; y++)
            {
                var o = _cells[x, y].OneWayDoors;
                foreach (var d in AllDirections)
                    if ((o & Cell.ToWallFlag(d)) != 0) count++;
            }
        return count;
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
