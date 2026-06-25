using BardsTale.Core.Dungeon;
using BardsTale.Core.Geometry;

namespace BardsTale.Core.Town;

/// <summary>Hand-builds the town square of Skara Brae and places its buildings.</summary>
public static class TownMapBuilder
{
    public const int Size = 11;

    public static TownMap Build()
    {
        var streets = new Maze("Skara Brae", Size, Size);

        // A couple of internal wall blocks give the square some structure to navigate.
        Block(streets, 3, 3);
        Block(streets, 7, 3);
        Block(streets, 3, 7);
        Block(streets, 7, 7);

        streets.SealBorders();

        var town = new TownMap(streets)
        {
            StartPosition = new Position(5, 5),
            StartFacing = Direction.North
        };

        town.AddBuilding(TownBuilding.Guild, "Adventurers Guild", new Position(5, 0));
        town.AddBuilding(TownBuilding.Tavern, "The Scarlet Bard", new Position(1, 0));
        town.AddBuilding(TownBuilding.Tavern, "Mad Mable's", new Position(9, 0));
        town.AddBuilding(TownBuilding.ReviewBoard, "Review Board", new Position(0, 5));
        town.AddBuilding(TownBuilding.Shop, "Garth's Equipment Shoppe", new Position(10, 5));
        town.AddBuilding(TownBuilding.Temple, "Temple of Healing", new Position(5, 10));
        town.AddBuilding(TownBuilding.Inn, "Garrick's Inn", new Position(1, 10));
        town.AddBuilding(TownBuilding.DungeonEntrance, "Catacomb Stair", new Position(10, 10));
        town.AddBuilding(TownBuilding.QuestBoard, "The Notice Board", new Position(0, 0));
        town.AddBuilding(TownBuilding.Smithy, "The Forge", new Position(0, 10));

        return town;
    }

    /// <summary>Walls a single cell off on all four sides to form an obstacle.</summary>
    private static void Block(Maze maze, int x, int y)
    {
        maze.SetWall(x, y, Direction.North);
        maze.SetWall(x, y, Direction.East);
        maze.SetWall(x, y, Direction.South);
        maze.SetWall(x, y, Direction.West);
    }
}
