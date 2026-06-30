using BardsTale.Core.Characters;
using BardsTale.Core.Items;
using BardsTale.Core.Magic;
using BardsTale.Core.Util;
using Attribute = BardsTale.Core.Characters.Attribute;

namespace BardsTale.Core.Game;

/// <summary>Experience and level-up rules, applied at the Review Board.</summary>
public static class Progression
{
    /// <summary>The level a hero must reach in their current class before they can retrain.</summary>
    public const int MinChangeClassLevel = 3;

    /// <summary>A hero gains a point in their class's prime attribute every this many levels.</summary>
    public const int AttributeGainInterval = 3;

    /// <summary>The ceiling a level-up attribute gain will not push a prime attribute past.</summary>
    public const int AttributeCap = 24;

    /// <summary>The attribute a class advances on the milestone levels — what makes that class tick.</summary>
    public static Attribute PrimeAttribute(CharacterClass c) => c switch
    {
        CharacterClass.Warrior or CharacterClass.Paladin => Attribute.Strength,
        CharacterClass.Rogue or CharacterClass.Hunter or CharacterClass.Monk => Attribute.Dexterity,
        CharacterClass.Bard => Attribute.Luck,
        _ => Attribute.Intelligence // the four spellcasting schools
    };

    // A drained hero must have their levels restored at the Temple before they can advance again.
    public static bool CanLevelUp(Character c) => !c.IsDead && !c.IsDrained && c.Experience >= c.ExperienceForNextLevel;

    /// <summary>Whether a hero may retrain into <paramref name="newClass"/> right now.</summary>
    public static bool CanChangeClass(Character c, CharacterClass newClass, out string reason)
    {
        reason = "";
        if (c.IsDead) { reason = $"{c.Name} must be revived first."; return false; }
        if (c.IsDrained) { reason = $"Restore {c.Name}'s drained levels at the Temple first."; return false; }
        if (c.Class == newClass) { reason = $"{c.Name} is already a {Classes.Get(newClass).Name}."; return false; }
        if (c.Level < MinChangeClassLevel)
        {
            reason = $"{c.Name} must reach level {MinChangeClassLevel} before changing class.";
            return false;
        }
        return true;
    }

    /// <summary>
    /// Retrains a hero into a new class: they restart at level 1 but <b>keep their
    /// accumulated hit points and spell points</b> — the cross-class reward — and learn
    /// the new vocation's starting spells/songs on top of anything already known.
    /// Returns any equipped gear the new class cannot use, for the caller to stash.
    /// </summary>
    public static IReadOnlyList<Item> ChangeClass(Character c, CharacterClass newClass)
    {
        c.Class = newClass;
        c.Level = 1;
        c.Experience = 0;

        var def = Classes.Get(newClass);
        // HP/SP carry over; a brand-new caster gets at least a level-1 spell pool.
        if (def.IsSpellcaster)
            c.MaxSpellPoints = Math.Max(c.MaxSpellPoints, Math.Max(1, c.Attributes.Intelligence / 2));
        c.HitPoints = c.MaxHitPoints;
        c.SpellPoints = c.MaxSpellPoints;

        if (def.IsSpellcaster)
            foreach (var spell in Spells.KnownAtLevel(def.School, 1))
                if (!c.KnownSpells.Contains(spell.Id))
                    c.KnownSpells.Add(spell.Id);
        if (newClass == CharacterClass.Bard)
            foreach (var song in Songs.KnownAtLevel(1))
                if (!c.KnownSongs.Contains(song.Id))
                    c.KnownSongs.Add(song.Id);

        var displaced = new List<Item>();
        if (c.Weapon is { } w && !Equipment.CanEquip(c, w, out _)) { displaced.Add(w); c.Weapon = null; }
        if (c.Armor is { } a && !Equipment.CanEquip(c, a, out _)) { displaced.Add(a); c.Armor = null; }
        if (c.Shield is { } s && !Equipment.CanEquip(c, s, out _)) { displaced.Add(s); c.Shield = null; }
        return displaced;
    }

    /// <summary>
    /// Advances a character one level if they have the experience, raising HP/SP.
    /// Returns a description of what happened, or null if they cannot yet advance.
    /// </summary>
    public static string? TryLevelUp(Character c, IRandomSource rng)
    {
        if (!CanLevelUp(c)) return null;

        var attacksBefore = c.AttacksPerRound;

        c.Experience -= c.ExperienceForNextLevel;
        c.Level++;

        var conBonus = (c.Attributes.Constitution - 12) / 4;
        var hpGain = Math.Max(1, rng.Roll(1, c.Definition.HitDieSides, conBonus));
        c.MaxHitPoints += hpGain;
        c.HitPoints += hpGain;

        var detail = $"+{hpGain} HP";
        if (c.IsSpellcaster)
        {
            var spGain = Math.Max(1, c.Attributes.Intelligence / 4);
            c.MaxSpellPoints += spGain;
            c.SpellPoints += spGain;
            detail += $", +{spGain} SP";

            var learned = LearnNewSpells(c);
            if (learned.Count > 0)
                detail += $", learns {string.Join(", ", learned)}";
        }

        if (c.IsBard)
        {
            var newSongs = LearnNewSongs(c);
            if (newSongs.Count > 0)
                detail += $", learns {string.Join(", ", newSongs)}";
        }

        // Every few levels, a point in the class's prime attribute — flowing through damage, AC,
        // spell points, saves and the like, so higher levels grow the whole hero, not just HP/SP.
        if (c.Level % AttributeGainInterval == 0)
        {
            var attr = PrimeAttribute(c.Class);
            if (c.Attributes[attr] < AttributeCap)
            {
                c.Attributes[attr]++;
                detail += $", +1 {AttributeSet.Abbreviation(attr)}";
            }
        }

        // Martial classes earn extra swings as they advance (AttacksPerRound) — call it out when it ticks up.
        if (c.AttacksPerRound > attacksBefore)
            detail += ", gains an extra attack each round!";

        return $"{c.Name} advances to level {c.Level}! ({detail})";
    }

    private static List<string> LearnNewSpells(Character c)
    {
        var learned = new List<string>();
        foreach (var spell in Spells.LearnedAtLevel(c.School, c.Level))
        {
            if (c.KnownSpells.Contains(spell.Id)) continue;
            c.KnownSpells.Add(spell.Id);
            learned.Add(spell.Name);
        }
        return learned;
    }

    private static List<string> LearnNewSongs(Character c)
    {
        var learned = new List<string>();
        foreach (var song in Songs.LearnedAtLevel(c.Level))
        {
            if (c.KnownSongs.Contains(song.Id)) continue;
            c.KnownSongs.Add(song.Id);
            learned.Add(song.Name);
        }
        return learned;
    }
}
