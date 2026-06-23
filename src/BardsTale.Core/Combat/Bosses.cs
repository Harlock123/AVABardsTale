namespace BardsTale.Core.Combat;

/// <summary>The fixed lair bosses that guard the dungeon, and the encounters they lead.</summary>
public static class Bosses
{
    public static readonly MonsterTemplate SkeletonLord =
        new("Skeleton Lord", 55, 4, 2, 6, 3, 400, 120, 1, Speed: 3,
            Ability: MonsterAbility.DrainLevel, AbilityChance: 0.25);

    public static readonly MonsterTemplate CovenMatron =
        new("Coven Matron", 48, 5, 1, 8, 2, 450, 150, 1, Speed: 3,
            Spell: new MonsterSpell("Mass Slumber", MonsterSpellKind.SleepFoe, Power: 3, Chance: 0.50));

    public static readonly MonsterTemplate CryptTyrant =
        new("Crypt Tyrant", 70, 3, 2, 8, 4, 600, 200, 1, Speed: 2,
            Ability: MonsterAbility.DrainStat, AbilityChance: 0.30);

    /// <summary>The Mad Wizard himself — the final boss at the bottom of the catacombs.</summary>
    public static readonly MonsterTemplate Mangar =
        new("Mangar the Mad", 120, 2, 2, 8, 4, 2000, 1000, 1, Speed: 4,
            Spell: new MonsterSpell("Mind Storm", MonsterSpellKind.BlastParty, Power: 12, Chance: 0.50),
            Ability: MonsterAbility.DrainLevel, AbilityChance: 0.30);

    /// <summary>The level on which Mangar lairs. Beating him wins the game.</summary>
    public const int FinalDepth = 3;

    // The early lairs: a boss flanked by themed minions, one per depth before the final.
    private static readonly (MonsterTemplate Boss, MonsterTemplate Minion, int MinionCount)[] Lairs =
    {
        (SkeletonLord, Bestiary.Skeleton, 3),
        (CovenMatron, Bestiary.CovenWitch, 2),
    };

    public static MonsterTemplate BossForDepth(int depth) =>
        depth >= FinalDepth ? Mangar : Lairs[(Math.Max(1, depth) - 1) % Lairs.Length].Boss;

    /// <summary>Builds the fixed boss encounter for a dungeon level — Mangar's at the bottom.</summary>
    public static Encounter Create(int depth)
    {
        if (depth >= FinalDepth)
            return new Encounter(new[]
            {
                new MonsterGroup(Mangar, 1),
                new MonsterGroup(Bestiary.Wraith, 2)
            }, isBoss: true, isFinalBoss: true);

        var (boss, minion, count) = Lairs[(Math.Max(1, depth) - 1) % Lairs.Length];
        return new Encounter(new[]
        {
            new MonsterGroup(boss, 1),
            new MonsterGroup(minion, count)
        }, isBoss: true);
    }
}
