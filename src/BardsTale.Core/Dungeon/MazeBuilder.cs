using BardsTale.Core.Geometry;
using BardsTale.Core.Util;

namespace BardsTale.Core.Dungeon;

/// <summary>
/// Generates a connected maze level using a recursive-backtracker carve, then
/// opens a few extra passages so the result reads as a dungeon rather than a
/// perfect maze. Finishes by placing stairs and a flavour message.
/// </summary>
public sealed class MazeBuilder
{
    private readonly IRandomSource _rng;

    public MazeBuilder(IRandomSource rng) => _rng = rng;

    public Maze Build(string name, int width, int height)
    {
        var maze = new Maze(name, width, height);

        // Start with every internal wall raised, then carve.
        FillAllWalls(maze);
        Carve(maze, width, height);
        OpenExtraPassages(maze, width, height);
        maze.SealBorders();

        maze.StartPosition = new Position(width / 2, height - 1);
        maze[maze.StartPosition].Feature = CellFeature.StairsUp;
        maze.StartFacing = Direction.North;

        PlaceFeature(maze, CellFeature.StairsDown, new Position(width / 2, 0));
        var msg = new Position(0, height / 2);
        maze[msg].Feature = CellFeature.Message;
        maze[msg].Text = "Crude runes are scratched into the stone: \"TURN BACK, FOOLS.\"";

        // A boss lair guards the descent, just before the downward stair.
        var lair = new Position(width / 2, 1);
        if (maze.InBounds(lair) && maze[lair].Feature == CellFeature.None)
            maze[lair].Feature = CellFeature.BossLair;

        PlaceSpecialTiles(maze, width, height);
        return maze;
    }

    /// <summary>Scatters spinners, traps, a teleporter and a patch of darkness across the level.</summary>
    private void PlaceSpecialTiles(Maze maze, int width, int height)
    {
        var empty = new List<Position>();
        for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
                if (maze[x, y].Feature == CellFeature.None)
                    empty.Add(new Position(x, y));

        Position? Take()
        {
            if (empty.Count == 0) return null;
            var i = _rng.Next(0, empty.Count);
            var p = empty[i];
            empty.RemoveAt(i);
            return p;
        }

        void Place(CellFeature feature, int count)
        {
            for (var i = 0; i < count; i++)
                if (Take() is { } p)
                    maze[p].Feature = feature;
        }

        Place(CellFeature.SpinnerTrap, 2);
        Place(CellFeature.Trap, 2);
        Place(CellFeature.Darkness, 3);
        Place(CellFeature.AntiMagic, 2);
        // A couple of treasure chests reward the bold (and the party's Rogue),
        // with one rarer gilded chest holding a guaranteed warding accessory.
        Place(CellFeature.Chest, 2);
        Place(CellFeature.OrnateChest, 1);
        // An inscribed riddle tile, answerable for a reward.
        if (Take() is { } riddle)
        {
            maze[riddle].Feature = CellFeature.Riddle;
            maze[riddle].RiddleId = _rng.Next(0, Riddles.All.Count);
        }

        // A non-combat dungeon event — a scene with choices, met between the fights.
        if (Take() is { } evt)
        {
            maze[evt].Feature = CellFeature.Event;
            maze[evt].EventId = _rng.Next(0, DungeonEvents.Count);
        }

        PlaceSecretVaults(maze, width, height, count: 2);

        if (Take() is { } tele && Take() is { } dest)
        {
            maze[tele].Feature = CellFeature.Teleporter;
            maze[tele].Destination = dest;
        }

        // A barred vault, sealed behind a portcullis. Placed last so nothing clobbers it:
        // the lever that raises it sits out in the open maze, making a find-the-mechanism puzzle.
        PlaceLeverVault(maze, width, height, Take);

        // A locked vault: sealed behind a locked door, with its iron key dropped elsewhere on
        // the floor — find and carry the key to open it.
        PlaceKeyedVault(maze, width, height, Take);
    }

    /// <summary>
    /// Seals a dead-end cell behind a locked door and stocks it with treasure, then drops the
    /// iron key that opens it on an open tile elsewhere. Like the lever vault, sealing a dead-end
    /// never strands the maze, and the key is always reachable without one.
    /// </summary>
    private void PlaceKeyedVault(Maze maze, int width, int height, Func<Position?> take)
    {
        var dirs = new[] { Direction.North, Direction.East, Direction.South, Direction.West };

        var leaves = new List<Position>();
        for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
            {
                var p = new Position(x, y);
                if (maze[p].Feature != CellFeature.None) continue;
                if (OpenPassages(maze, p, dirs).Count == 1) leaves.Add(p);
            }
        if (leaves.Count == 0) return;

        if (take() is not { } key) return; // no room for the key — skip the puzzle, never seal blind
        leaves.Remove(key);
        if (leaves.Count == 0) return;

        var pos = leaves[_rng.Next(0, leaves.Count)];
        var open = OpenPassages(maze, pos, dirs);
        if (open.Count != 1) return;

        maze.MarkLockedDoor(pos.X, pos.Y, open[0]);
        maze[pos].Feature = _rng.Chance(0.4) ? CellFeature.OrnateChest : CellFeature.Chest;
        maze[key].Feature = CellFeature.Key;
    }

    /// <summary>
    /// Seals a dead-end cell behind a barred gate and stocks it with treasure, then drops a rune
    /// lever on an open tile elsewhere. The vault can only be reached by finding and pulling the
    /// lever (which raises every gate on the level). Sealing a dead-end never strands the maze.
    /// </summary>
    private void PlaceLeverVault(Maze maze, int width, int height, Func<Position?> take)
    {
        var dirs = new[] { Direction.North, Direction.East, Direction.South, Direction.West };

        var leaves = new List<Position>();
        for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
            {
                var p = new Position(x, y);
                if (maze[p].Feature != CellFeature.None) continue;
                if (OpenPassages(maze, p, dirs).Count == 1) leaves.Add(p);
            }
        if (leaves.Count == 0) return;

        // Reserve the lever's tile first; with no room for a lever, skip the puzzle entirely
        // rather than seal an unreachable vault.
        if (take() is not { } lever) return;

        leaves.Remove(lever); // never put the lever inside the vault it opens
        if (leaves.Count == 0) return;

        var pos = leaves[_rng.Next(0, leaves.Count)];
        var open = OpenPassages(maze, pos, dirs);
        if (open.Count != 1) return;

        maze.MarkGate(pos.X, pos.Y, open[0]);
        maze[pos].Feature = _rng.Chance(0.4) ? CellFeature.OrnateChest : CellFeature.Chest;
        maze[lever].Feature = CellFeature.Lever;
    }

    /// <summary>
    /// Turns a few dead-end cells into hidden vaults: seals the cell's single passage into a
    /// secret door and stocks it with treasure, so it can only be reached by searching it out.
    /// Dead-ends carry no through-traffic, so sealing them never strands the rest of the maze.
    /// </summary>
    private void PlaceSecretVaults(Maze maze, int width, int height, int count)
    {
        var dirs = new[] { Direction.North, Direction.East, Direction.South, Direction.West };

        var leaves = new List<Position>();
        for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
            {
                var p = new Position(x, y);
                if (maze[p].Feature != CellFeature.None) continue;
                if (OpenPassages(maze, p, dirs).Count == 1) leaves.Add(p);
            }

        for (var i = 0; i < count && leaves.Count > 0; i++)
        {
            var idx = _rng.Next(0, leaves.Count);
            var pos = leaves[idx];
            leaves.RemoveAt(idx);

            var open = OpenPassages(maze, pos, dirs);
            if (open.Count != 1) continue; // a previous vault may have changed this one

            maze.MarkSecretDoor(pos.X, pos.Y, open[0]);
            maze[pos].Feature = _rng.Chance(0.35) ? CellFeature.OrnateChest : CellFeature.Chest;
        }
    }

    private static List<Direction> OpenPassages(Maze maze, Position p, Direction[] dirs) =>
        dirs.Where(d => !maze[p].HasWall(d) && maze.InBounds(p.Step(d))).ToList();

    private static void FillAllWalls(Maze maze)
    {
        for (var x = 0; x < maze.Width; x++)
            for (var y = 0; y < maze.Height; y++)
                maze[x, y].Walls = Walls.North | Walls.East | Walls.South | Walls.West;
    }

    private void Carve(Maze maze, int width, int height)
    {
        var visited = new bool[width, height];
        var stack = new Stack<Position>();
        var start = new Position(0, 0);
        visited[start.X, start.Y] = true;
        stack.Push(start);

        var dirs = new[] { Direction.North, Direction.East, Direction.South, Direction.West };

        while (stack.Count > 0)
        {
            var current = stack.Peek();
            var unvisited = new List<Direction>();
            foreach (var d in dirs)
            {
                var n = current.Step(d);
                if (maze.InBounds(n) && !visited[n.X, n.Y])
                    unvisited.Add(d);
            }

            if (unvisited.Count == 0)
            {
                stack.Pop();
                continue;
            }

            var dir = _rng.Pick(unvisited);
            maze.SetWall(current.X, current.Y, dir, present: false);
            var next = current.Step(dir);
            visited[next.X, next.Y] = true;
            stack.Push(next);
        }
    }

    /// <summary>Knock out roughly one extra wall in eight to create loops and rooms.</summary>
    private void OpenExtraPassages(Maze maze, int width, int height)
    {
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                if (!_rng.Chance(0.12)) continue;
                var dir = _rng.Pick(new[] { Direction.North, Direction.East, Direction.South, Direction.West });
                var n = new Position(x, y).Step(dir);
                if (maze.InBounds(n))
                    maze.SetWall(x, y, dir, present: false);
            }
        }
    }

    private static void PlaceFeature(Maze maze, CellFeature feature, Position p)
    {
        if (maze.InBounds(p))
            maze[p].Feature = feature;
    }
}
