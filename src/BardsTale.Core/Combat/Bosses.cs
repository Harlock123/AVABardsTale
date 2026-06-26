using BardsTale.Core.Characters;

namespace BardsTale.Core.Combat;

/// <summary>The fixed lair bosses that guard each floor of the dungeon, and the encounters they lead.</summary>
public static class Bosses
{
    public static readonly MonsterTemplate SkeletonLord =
        new("Skeleton Lord", 55, 4, 2, 6, 3, 400, 120, 1, Speed: 3,
            Ability: MonsterAbility.DrainLevel, AbilityChance: 0.25);

    public static readonly MonsterTemplate CovenMatron =
        new("Coven Matron", 48, 5, 1, 8, 2, 450, 150, 1, Speed: 3,
            Spell: new MonsterSpell("Mass Slumber", MonsterSpellKind.SleepFoe, Power: 3, Chance: 0.50));

    public static readonly MonsterTemplate GoblinKing =
        new("Goblin King", 70, 6, 2, 6, 1, 500, 160, 1, Speed: 3);

    public static readonly MonsterTemplate CryptTyrant =
        new("Crypt Tyrant", 70, 3, 2, 8, 4, 600, 200, 1, Speed: 2,
            Ability: MonsterAbility.DrainStat, AbilityChance: 0.30);

    public static readonly MonsterTemplate OrcWarlord =
        new("Orc Warlord", 90, 5, 2, 8, 2, 650, 220, 1, Speed: 3);

    public static readonly MonsterTemplate Medusa =
        new("Medusa", 85, 4, 2, 6, 0, 700, 240, 1, Speed: 3,
            InflictsStatus: StatusEffect.Paralyzed, StatusChance: 0.40);

    public static readonly MonsterTemplate DemonLord =
        new("Demon Lord", 95, 2, 2, 8, 4, 900, 400, 1, Speed: 3,
            Spell: new MonsterSpell("Hellstorm", MonsterSpellKind.BlastParty, Power: 14, Chance: 0.50),
            Ability: MonsterAbility.DrainLevel, AbilityChance: 0.25);

    public static readonly MonsterTemplate TrollKing =
        new("Troll King", 130, 4, 3, 6, 0, 900, 300, 1);

    public static readonly MonsterTemplate WerewolfAlpha =
        new("Werewolf Alpha", 110, 4, 2, 8, 2, 850, 260, 1, Speed: 5);

    public static readonly MonsterTemplate VampireLord =
        new("Vampire Lord", 125, 2, 2, 8, 0, 1000, 400, 1, Speed: 4,
            Ability: MonsterAbility.DrainLevel, AbilityChance: 0.35);

    public static readonly MonsterTemplate StoneTitan =
        new("Stone Titan", 160, 1, 3, 8, 0, 1100, 350, 1, Speed: 1);

    public static readonly MonsterTemplate LichKing =
        new("Lich King", 140, 1, 2, 10, 0, 1200, 600, 1, Speed: 2,
            Spell: new MonsterSpell("Raise Dead", MonsterSpellKind.Summon, Power: 3, Chance: 0.50, SummonTemplate: Bestiary.Skeleton),
            Ability: MonsterAbility.DrainLevel, AbilityChance: 0.30);

    public static readonly MonsterTemplate WyvernMatriarch =
        new("Wyvern Matriarch", 150, 3, 3, 6, 0, 1100, 350, 1, Speed: 5,
            InflictsStatus: StatusEffect.Poisoned, StatusChance: 0.35);

    public static readonly MonsterTemplate BeholderTyrant =
        new("Beholder Tyrant", 150, 0, 2, 8, 0, 1300, 500, 1, Speed: 2,
            Spell: new MonsterSpell("Disintegrate", MonsterSpellKind.DamageFoe, Power: 28, Chance: 0.60));

    public static readonly MonsterTemplate FrostKing =
        new("Frost King", 180, 3, 3, 8, 0, 1300, 450, 1, Speed: 3,
            Spell: new MonsterSpell("Blizzard", MonsterSpellKind.BlastParty, Power: 22, Chance: 0.55));

    public static readonly MonsterTemplate DeathTyrant =
        new("Death Tyrant", 175, 1, 2, 10, 0, 1400, 500, 1, Speed: 2,
            Ability: MonsterAbility.DrainLevel, AbilityChance: 0.35);

    public static readonly MonsterTemplate PitLord =
        new("Pit Lord", 200, 0, 3, 8, 0, 1500, 600, 1, Speed: 4,
            Spell: new MonsterSpell("Hellfire", MonsterSpellKind.BlastParty, Power: 26, Chance: 0.55),
            Ability: MonsterAbility.DrainLevel, AbilityChance: 0.30);

    public static readonly MonsterTemplate Archlich =
        new("Archlich", 200, 0, 2, 10, 0, 1500, 800, 1, Speed: 2,
            Spell: new MonsterSpell("Soul Reap", MonsterSpellKind.DamageFoe, Power: 32, Chance: 0.60),
            Ability: MonsterAbility.DrainLevel, AbilityChance: 0.35);

    public static readonly MonsterTemplate DragonTyrant =
        new("Dragon Tyrant", 240, 0, 4, 8, 0, 1700, 900, 1, Speed: 4,
            Spell: new MonsterSpell("Cataclysm Breath", MonsterSpellKind.BlastParty, Power: 30, Chance: 0.60));

    /// <summary>The Mad Wizard himself — the final boss at the bottom of the catacombs.</summary>
    public static readonly MonsterTemplate Mangar =
        new("Mangar the Mad", 300, 1, 3, 8, 0, 5000, 2000, 1, Speed: 5,
            Spell: new MonsterSpell("Mind Storm", MonsterSpellKind.BlastParty, Power: 28, Chance: 0.55),
            Ability: MonsterAbility.DrainLevel, AbilityChance: 0.35);

    /// <summary>Every named boss, for the bestiary catalogue.</summary>
    public static readonly IReadOnlyList<MonsterTemplate> All = new[]
    {
        SkeletonLord, CovenMatron, GoblinKing, CryptTyrant, OrcWarlord, Medusa, DemonLord, TrollKing,
        WerewolfAlpha, VampireLord, StoneTitan, LichKing, WyvernMatriarch, BeholderTyrant, FrostKing,
        DeathTyrant, PitLord, Archlich, DragonTyrant, Mangar
    };

    /// <summary>The level on which Mangar lairs. Beating him wins the game.</summary>
    public const int FinalDepth = 20;

    // One lair per floor (1..FinalDepth-1), a boss flanked by themed minions, ramping in power.
    private static readonly (MonsterTemplate Boss, MonsterTemplate Minion, int MinionCount)[] Lairs =
    {
        (SkeletonLord, Bestiary.Skeleton, 3),
        (CovenMatron, Bestiary.CovenWitch, 2),
        (GoblinKing, Bestiary.Goblin, 4),
        (CryptTyrant, Bestiary.CryptCrawler, 2),
        (OrcWarlord, Bestiary.Orc, 3),
        (Medusa, Bestiary.GiantScorpion, 2),
        (DemonLord, Bestiary.FlameShade, 2),
        (TrollKing, Bestiary.Ogre, 2),
        (WerewolfAlpha, Bestiary.Werewolf, 2),
        (VampireLord, Bestiary.Wight, 2),
        (StoneTitan, Bestiary.Gargoyle, 2),
        (LichKing, Bestiary.Wraith, 2),
        (WyvernMatriarch, Bestiary.Wyvern, 1),
        (BeholderTyrant, Bestiary.EyeTyrant, 1),
        (FrostKing, Bestiary.FrostGiant, 1),
        (DeathTyrant, Bestiary.DeathKnight, 1),
        (PitLord, Bestiary.PitFiend, 1),
        (Archlich, Bestiary.Lich, 1),
        (DragonTyrant, Bestiary.YoungRedDragon, 1),
    };

    public static MonsterTemplate BossForDepth(int depth) =>
        depth >= FinalDepth ? Mangar : Lairs[(Math.Max(1, depth) - 1) % Lairs.Length].Boss;

    /// <summary>The floor a named boss lairs on, or null if the name isn't a lair boss.</summary>
    public static int? FloorOf(string bossName)
    {
        for (var i = 0; i < Lairs.Length; i++)
            if (Lairs[i].Boss.Name == bossName) return i + 1;
        return bossName == Mangar.Name ? FinalDepth : null;
    }

    /// <summary>Builds the fixed boss encounter for a dungeon level — Mangar's at the bottom. Scales for New Game+ and difficulty.</summary>
    public static Encounter Create(int depth, int ascension = 0, DifficultyProfile? difficulty = null)
    {
        if (depth >= FinalDepth)
            return new Encounter(new[]
            {
                new MonsterGroup(NgPlus.Scale(Mangar, ascension, difficulty), 1),
                new MonsterGroup(NgPlus.Scale(Bestiary.Wraith, ascension, difficulty), 2)
            }, isBoss: true, isFinalBoss: true);

        var (boss, minion, count) = Lairs[(Math.Max(1, depth) - 1) % Lairs.Length];
        return new Encounter(new[]
        {
            new MonsterGroup(NgPlus.Scale(boss, ascension, difficulty), 1),
            new MonsterGroup(NgPlus.Scale(minion, ascension, difficulty), count)
        }, isBoss: true);
    }
}
