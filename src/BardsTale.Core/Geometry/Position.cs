namespace BardsTale.Core.Geometry;

/// <summary>Integer grid coordinate inside a maze level.</summary>
public readonly record struct Position(int X, int Y)
{
    public Position Step(Direction dir)
    {
        var (dx, dy) = dir.Delta();
        return new Position(X + dx, Y + dy);
    }

    public override string ToString() => $"({X}, {Y})";
}
