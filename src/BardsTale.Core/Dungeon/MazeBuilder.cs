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
        // A couple of treasure chests reward the bold (and the party's Rogue).
        Place(CellFeature.Chest, 2);

        if (Take() is { } tele && Take() is { } dest)
        {
            maze[tele].Feature = CellFeature.Teleporter;
            maze[tele].Destination = dest;
        }
    }

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
