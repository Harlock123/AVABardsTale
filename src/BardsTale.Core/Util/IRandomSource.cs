namespace BardsTale.Core.Util;

/// <summary>
/// Abstraction over randomness so combat and encounters can be made deterministic in tests.
/// </summary>
public interface IRandomSource
{
    /// <summary>Inclusive lower bound, exclusive upper bound.</summary>
    int Next(int minInclusive, int maxExclusive);

    double NextDouble();
}

public sealed class SystemRandomSource : IRandomSource
{
    private readonly Random _random;

    public SystemRandomSource(int? seed = null)
        => _random = seed is null ? new Random() : new Random(seed.Value);

    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

    public double NextDouble() => _random.NextDouble();
}

public static class RandomSourceExtensions
{
    /// <summary>Rolls <paramref name="count"/> dice of <paramref name="sides"/> sides each, plus a flat bonus.</summary>
    public static int Roll(this IRandomSource rng, int count, int sides, int bonus = 0)
    {
        var total = bonus;
        for (var i = 0; i < count; i++)
            total += rng.Next(1, sides + 1);
        return total;
    }

    public static bool Chance(this IRandomSource rng, double probability) => rng.NextDouble() < probability;

    public static T Pick<T>(this IRandomSource rng, IReadOnlyList<T> items) => items[rng.Next(0, items.Count)];
}
