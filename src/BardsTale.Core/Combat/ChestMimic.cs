using BardsTale.Core.Characters;

namespace BardsTale.Core.Combat;

/// <summary>
/// The mimic: a predator that disguises itself as a treasure chest. A party that opens
/// the wrong chest finds teeth instead of gold. Its stats scale with the depth it lurks
/// at; the bestiary lists a single representative entry (all mimics share the one name).
/// </summary>
public static class ChestMimic
{
    /// <summary>The bestiary's representative mimic — combat uses a depth-scaled version of this.</summary>
    public static readonly MonsterTemplate Template = Make(6);

    private static MonsterTemplate Make(int depth) => new(
        Name: "Mimic",
        MaxHitPoints: 24 + depth * 7,
        ArmorClass: 4,
        AttackDice: 1,
        AttackSides: 6 + depth / 4,
        AttackBonus: 1 + depth / 3,
        ExperienceValue: 50 + depth * 18,
        GoldValue: 40 + depth * 12,
        MaxPerGroup: 1,
        Speed: 3,
        InflictsStatus: StatusEffect.Paralyzed,
        StatusChance: 0.25);

    /// <summary>A lone mimic, scaled to the given dungeon depth.</summary>
    public static Encounter EncounterFor(int depth) => new(new[] { new MonsterGroup(Make(depth), 1) });
}
