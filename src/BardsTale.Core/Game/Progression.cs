using BardsTale.Core.Characters;
using BardsTale.Core.Magic;
using BardsTale.Core.Util;

namespace BardsTale.Core.Game;

/// <summary>Experience and level-up rules, applied at the Review Board.</summary>
public static class Progression
{
    // A drained hero must have their levels restored at the Temple before they can advance again.
    public static bool CanLevelUp(Character c) => !c.IsDead && !c.IsDrained && c.Experience >= c.ExperienceForNextLevel;

    /// <summary>
    /// Advances a character one level if they have the experience, raising HP/SP.
    /// Returns a description of what happened, or null if they cannot yet advance.
    /// </summary>
    public static string? TryLevelUp(Character c, IRandomSource rng)
    {
        if (!CanLevelUp(c)) return null;

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
