using BardsTale.Core.Combat;
using BardsTale.Core.Items;

namespace BardsTale.Core.Characters;

[Flags]
public enum StatusEffect
{
    None = 0,
    Poisoned = 1 << 0,
    Paralyzed = 1 << 1,
    Asleep = 1 << 2,
    Dead = 1 << 3
}

/// <summary>
/// A single adventurer. Holds attributes, derived combat stats, equipment and
/// progression. UI layers observe this through view models rather than directly.
/// </summary>
public sealed class Character
{
    public required string Name { get; set; }
    public required Race Race { get; init; }
    public required CharacterClass Class { get; set; }
    public required AttributeSet Attributes { get; init; }

    public int Level { get; set; } = 1;
    public long Experience { get; set; }
    public int Gold { get; set; }

    public int MaxHitPoints { get; set; }
    public int HitPoints { get; set; }
    public int MaxSpellPoints { get; set; }
    public int SpellPoints { get; set; }

    public Item? Weapon { get; set; }
    public Item? Armor { get; set; }
    public Item? Shield { get; set; }

    // Worn accessories: two ring slots and one amulet slot.
    public Item? Ring1 { get; set; }
    public Item? Ring2 { get; set; }
    public Item? Amulet { get; set; }

    /// <summary>Every equipped accessory, skipping empty slots.</summary>
    public IEnumerable<Item> Accessories
    {
        get
        {
            if (Ring1 is not null) yield return Ring1;
            if (Ring2 is not null) yield return Ring2;
            if (Amulet is not null) yield return Amulet;
        }
    }

    /// <summary>The accessory set bonuses currently active (every piece worn).</summary>
    public IReadOnlyList<AccessorySet> ActiveSets => AccessorySets.ActiveFor(this).ToList();

    /// <summary>Hints for sets this character is one accessory away from completing.</summary>
    public IReadOnlyList<string> SetHints => AccessorySets.HintsFor(this).ToList();

    public StatusEffect Status { get; set; } = StatusEffect.None;

    // Levels (and the HP/SP they granted) sapped by level drain, awaiting restoration.
    public int DrainedLevels { get; set; }
    public int DrainedHitPoints { get; set; }
    public int DrainedSpellPoints { get; set; }
    public bool IsDrained => DrainedLevels > 0;

    /// <summary>Attribute points sapped by stat drain, awaiting restoration at the Temple.</summary>
    public AttributeSet DrainedAttributes { get; } = new();
    public int TotalDrainedStats => DrainedAttributes.Strength + DrainedAttributes.Intelligence
        + DrainedAttributes.Dexterity + DrainedAttributes.Constitution + DrainedAttributes.Luck;
    public bool HasDrainedStats => TotalDrainedStats > 0;

    /// <summary>Spell ids this character knows, in learn order.</summary>
    public List<string> KnownSpells { get; } = new();

    /// <summary>Song ids this character (a Bard) knows.</summary>
    public List<string> KnownSongs { get; } = new();

    /// <summary>How many fresh tunes a Bard can strike up before resting (refilled by resting/camping).</summary>
    public int BardTunes { get; set; }

    public ClassDefinition Definition => Classes.Get(Class);
    public MagicSchool School => Definition.School;
    public bool IsSpellcaster => Definition.IsSpellcaster;
    public bool IsBard => Class == CharacterClass.Bard;
    public bool CanSing => IsBard && KnownSongs.Count > 0;

    /// <summary>A Bard's maximum daily tunes — grows slowly with level.</summary>
    public int MaxBardTunes => IsBard ? 4 + Level / 2 : 0;

    /// <summary>Refills the Bard's tunes to full (on resting at the inn or making camp).</summary>
    public void RefreshBardTunes() { if (IsBard) BardTunes = MaxBardTunes; }

    public bool IsDead => Status.HasFlag(StatusEffect.Dead) || HitPoints <= 0;

    /// <summary>Can act this combat round: alive and not incapacitated.</summary>
    public bool CanAct => !IsDead
        && !Status.HasFlag(StatusEffect.Paralyzed)
        && !Status.HasFlag(StatusEffect.Asleep);

    public Item EffectiveWeapon => Weapon ?? Items.Items.Fists;

    /// <summary>True when the wielded weapon can strike from the back rank (a bow, sling or crossbow).</summary>
    public bool HasRangedWeapon => EffectiveWeapon.Ranged;

    /// <summary>
    /// Lower armour class is better, mirroring the original's AC convention.
    /// Base is 10, reduced by armour, shield and a dexterity bonus.
    /// </summary>
    public int ArmorClass
    {
        get
        {
            var ac = 10;
            ac -= Armor?.ArmorBonus ?? 0;
            ac -= Shield?.ArmorBonus ?? 0;
            ac -= Ring1?.ArmorBonus ?? 0;
            ac -= Ring2?.ArmorBonus ?? 0;
            ac -= Amulet?.ArmorBonus ?? 0;
            foreach (var set in ActiveSets) ac -= set.ArmorBonus;
            ac -= DexterityBonus;
            return ac;
        }
    }

    /// <summary>The elements this character wards against (taking half damage), drawn from equipped gear and set bonuses.</summary>
    public Element ResistedElements
    {
        get
        {
            var warded = (Armor?.ResistsElement ?? Element.None) | (Shield?.ResistsElement ?? Element.None);
            foreach (var accessory in Accessories)
                warded |= accessory.ResistsElement;
            foreach (var set in ActiveSets)
                warded |= set.WardBonus;
            return warded;
        }
    }

    /// <summary>True when equipped gear wards against the given attack element.</summary>
    public bool Resists(Element element) => element != Element.None && (ResistedElements & element) != 0;

    /// <summary>Accessory + set bonus to melee to-hit rolls.</summary>
    public int GearHitBonus => Accessories.Sum(a => a.HitBonus) + ActiveSets.Sum(s => s.HitBonus);

    /// <summary>Accessory + set bonus to melee damage.</summary>
    public int GearDamageBonus => Accessories.Sum(a => a.DamageBonus) + ActiveSets.Sum(s => s.DamageBonus);

    /// <summary>Accessory bonus to luck, sweetening saving throws.</summary>
    public int GearLuckBonus => Accessories.Sum(a => a.LuckBonus);

    /// <summary>Effective luck for saves: the base attribute plus any accessory bonus.</summary>
    public int EffectiveLuck => Attributes.Luck + GearLuckBonus;

    /// <summary>HP mended at the end of each combat round by regenerative accessories and sets.</summary>
    public int RegenPerRound => Accessories.Sum(a => a.RegenPerRound) + ActiveSets.Sum(s => s.RegenBonus);

    /// <summary>Status effects this character cannot be afflicted with, from accessories and sets.</summary>
    public StatusEffect ImmuneStatuses
    {
        get
        {
            var immune = StatusEffect.None;
            foreach (var accessory in Accessories) immune |= accessory.ImmuneStatus;
            foreach (var set in ActiveSets) immune |= set.ImmuneBonus;
            return immune;
        }
    }

    /// <summary>True when equipped gear makes this character immune to the given status.</summary>
    public bool IsImmuneTo(StatusEffect status) => (ImmuneStatuses & status) != 0;

    /// <summary>Bonus maximum hit points from equipped accessories and active sets.</summary>
    public int BonusMaxHitPoints => Accessories.Sum(a => a.MaxHitPointBonus) + ActiveSets.Sum(s => s.MaxHpBonus);

    /// <summary>Bonus maximum spell points from equipped accessories and active sets.</summary>
    public int BonusMaxSpellPoints => Accessories.Sum(a => a.MaxSpellPointBonus) + ActiveSets.Sum(s => s.MaxSpBonus);

    /// <summary>The hit-point ceiling once vitality gear is counted (the base max plus gear bonus).</summary>
    public int EffectiveMaxHitPoints => Math.Max(1, MaxHitPoints + BonusMaxHitPoints);

    /// <summary>The spell-point ceiling once gear is counted.</summary>
    public int EffectiveMaxSpellPoints => Math.Max(0, MaxSpellPoints + BonusMaxSpellPoints);

    /// <summary>
    /// Reconciles current HP/SP after an equipment change: gear that raised the ceiling
    /// grants that much extra vitality outright, and gear that lowered it clamps current
    /// values back down. Captures the previous effective maxes before the change.
    /// </summary>
    public void RescaleVitals(int previousEffectiveMaxHp, int previousEffectiveMaxSp)
    {
        var hpGain = EffectiveMaxHitPoints - previousEffectiveMaxHp;
        if (hpGain > 0 && !IsDead) HitPoints += hpGain;
        HitPoints = Math.Min(HitPoints, EffectiveMaxHitPoints);

        var spGain = EffectiveMaxSpellPoints - previousEffectiveMaxSp;
        if (spGain > 0) SpellPoints += spGain;
        SpellPoints = Math.Min(SpellPoints, EffectiveMaxSpellPoints);
    }

    public int DexterityBonus => (Attributes.Dexterity - 12) / 4;
    public int StrengthBonus => (Attributes.Strength - 12) / 4;

    /// <summary>Number of melee swings per round, scaling slowly with level by class.</summary>
    public int AttacksPerRound
    {
        get
        {
            var div = Definition.AttacksPerLevelDivisor;
            return Math.Clamp(1 + Level / div, 1, 4);
        }
    }

    /// <summary>Experience needed to reach the next level (simple geometric curve).</summary>
    public long ExperienceForNextLevel => (long)(1000 * Math.Pow(2, Level - 1));

    public void FullHeal()
    {
        if (IsDead) return;
        HitPoints = EffectiveMaxHitPoints;
        SpellPoints = EffectiveMaxSpellPoints;
    }

    public void ApplyDamage(int amount)
    {
        if (amount <= 0) return;
        HitPoints -= amount;
        if (HitPoints <= 0)
        {
            HitPoints = 0;
            Status |= StatusEffect.Dead;
        }
    }

    public void Heal(int amount)
    {
        if (IsDead) return;
        HitPoints = Math.Min(EffectiveMaxHitPoints, HitPoints + amount);
    }

    private const StatusEffect Ailments = StatusEffect.Poisoned | StatusEffect.Paralyzed | StatusEffect.Asleep;

    public bool HasAilment => (Status & Ailments) != 0;
    public bool IsPoisoned => Status.HasFlag(StatusEffect.Poisoned);
    public bool IsAsleep => Status.HasFlag(StatusEffect.Asleep);
    public bool IsParalyzed => Status.HasFlag(StatusEffect.Paralyzed);

    /// <summary>Inflicts a status on a living character — minus any the wearer's gear wards off entirely.</summary>
    public void Inflict(StatusEffect status)
    {
        status &= ~ImmuneStatuses;
        if (!IsDead && status != StatusEffect.None) Status |= status;
    }

    public void Wake() => Status &= ~StatusEffect.Asleep;

    /// <summary>Clears poison, paralysis and sleep (e.g. at the Temple or via a cure spell).</summary>
    public void CureAilments() => Status &= ~Ailments;

    /// <summary>
    /// Saps a level: lowers level and max HP/SP, remembering the loss so the Temple
    /// can restore it. While drained a character cannot advance at the Review Board.
    /// At level 1 there is no level to lose, so it bites into max HP instead.
    /// </summary>
    public void DrainLevel()
    {
        if (Level <= 1)
        {
            MaxHitPoints = Math.Max(1, MaxHitPoints - Math.Max(1, MaxHitPoints / 4));
            HitPoints = Math.Min(HitPoints, MaxHitPoints);
            return;
        }

        Level--;
        DrainedLevels++;

        var conBonus = (Attributes.Constitution - 12) / 4;
        var hpLoss = Math.Max(1, Definition.HitDieSides / 2 + conBonus);
        DrainedHitPoints += hpLoss;
        MaxHitPoints = Math.Max(1, MaxHitPoints - hpLoss);
        HitPoints = Math.Min(HitPoints, MaxHitPoints);

        if (IsSpellcaster)
        {
            var spLoss = Math.Max(1, Attributes.Intelligence / 4);
            DrainedSpellPoints += spLoss;
            MaxSpellPoints = Math.Max(0, MaxSpellPoints - spLoss);
            SpellPoints = Math.Min(SpellPoints, MaxSpellPoints);
        }
    }

    /// <summary>Undoes all level drain, restoring the lost levels and HP/SP, and makes the hero whole.</summary>
    public void RestoreLevels()
    {
        if (!IsDrained) return;
        Level += DrainedLevels;
        MaxHitPoints += DrainedHitPoints;
        MaxSpellPoints += DrainedSpellPoints;
        HitPoints = MaxHitPoints;
        SpellPoints = MaxSpellPoints;
        DrainedLevels = 0;
        DrainedHitPoints = 0;
        DrainedSpellPoints = 0;
    }

    /// <summary>Saps one point from an attribute (floored at 3), remembering the actual loss for restoration.</summary>
    public void DrainAttribute(Attribute attr)
    {
        var before = Attributes[attr];
        Attributes[attr] = Math.Max(3, before - 1);
        DrainedAttributes[attr] += before - Attributes[attr];
    }

    /// <summary>Restores all stat-drained attribute points at the Temple.</summary>
    public void RestoreStats()
    {
        if (!HasDrainedStats) return;
        foreach (Attribute a in Enum.GetValues<Attribute>())
        {
            Attributes[a] += DrainedAttributes[a];
            DrainedAttributes[a] = 0;
        }
    }

    /// <summary>Short tag for the roster: death and ailments take priority over wound state.</summary>
    public string StatusTag => IsDead ? "DEAD"
        : IsParalyzed ? "PARA"
        : IsAsleep ? "SLEEP"
        : IsPoisoned ? "POISON"
        : HitPoints <= EffectiveMaxHitPoints / 4 ? "HURT"
        : "OK";
}
