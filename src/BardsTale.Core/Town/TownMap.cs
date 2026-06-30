using BardsTale.Core.Dungeon;
using BardsTale.Core.Geometry;

namespace BardsTale.Core.Town;

public enum TownBuilding
{
    None,
    Guild,
    Shop,
    Temple,
    ReviewBoard,
    Tavern,
    Inn,
    DungeonEntrance,
    QuestBoard,
    Smithy,
    Bank,
    TowerEntrance
}

/// <summary>A building's door on the streets of Skara Brae.</summary>
public sealed record BuildingEntrance(TownBuilding Building, string Name, Position Position);

/// <summary>
/// The walkable overworld of Skara Brae: a street grid (a <see cref="Maze"/>) with
/// building entrances the party can step onto and enter.
/// </summary>
public sealed class TownMap
{
    private readonly Dictionary<Position, BuildingEntrance> _buildings = new();

    public TownMap(Maze streets) => Streets = streets;

    public Maze Streets { get; }
    public Position StartPosition { get; set; }
    public Direction StartFacing { get; set; } = Direction.North;

    public IReadOnlyCollection<BuildingEntrance> Buildings => _buildings.Values;

    public void AddBuilding(TownBuilding building, string name, Position position)
    {
        _buildings[position] = new BuildingEntrance(building, name, position);
        var cell = Streets[position];
        cell.Feature = CellFeature.Building;
        cell.Text = name;
    }

    public BuildingEntrance? BuildingAt(Position position)
        => _buildings.TryGetValue(position, out var entrance) ? entrance : null;
}
