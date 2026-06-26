using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using BardsTale.UI.Settings;

namespace BardsTale.UI.ViewModels;

/// <summary>The kind of combat-log line, used to pick its icon and colour.</summary>
public enum LogCategory
{
    Info, PartyHit, EnemyHit, Heal, Ward, EnemyDown, Grave, Miss, Status, Cast, Victory, Enrage
}

/// <summary>
/// One line of the combat log, classified by what happened so the view can colour and
/// icon it — party hits, blows taken, heals/regen, wards & saves, kills, deaths, misses
/// and casts each read distinctly. Classification is keyword-based; the one ambiguous
/// case ("X hits Y for N") is resolved by checking whether X is a party member. The colour
/// comes from <see cref="CombatLogPalette"/>, which honours the colourblind-friendly setting.
/// </summary>
public sealed class CombatLogLineViewModel
{
    public CombatLogLineViewModel(string text, ICollection<string> partyNames)
    {
        Text = text;
        (Icon, Category) = Classify(text, partyNames);
        Brush = CombatLogPalette.BrushFor(Category, AppSettings.Current.ColorblindMode);
    }

    public string Text { get; }
    public string Icon { get; }
    public LogCategory Category { get; }
    public IBrush Brush { get; }

    public bool HasIcon => Icon.Length > 0;

    private static (string Icon, LogCategory Category) Classify(string line, ICollection<string> partyNames)
    {
        bool Has(string s) => line.Contains(s, StringComparison.OrdinalIgnoreCase);
        bool ByParty() => partyNames.Any(n => n.Length > 0 && line.StartsWith(n, StringComparison.Ordinal));

        // Deaths — an enemy felled reads as a win, an ally lost reads as a loss.
        if (Has("is slain")) return ("☠", LogCategory.EnemyDown);
        if (Has("has fallen") || Has("succumbs")) return ("⚰", LogCategory.Grave);

        // Victory / defeat banners.
        if (Has("are defeated")) return ("🏆", LogCategory.Victory);

        // A boss or elite turning berserk — a danger cue the player should feel.
        if (Has("in fury") || Has("frenzy") || Has("enrage")) return ("💢", LogCategory.Enrage);

        // Wards, saves and resists — the standout the player wants to notice.
        if (Has("warded") || Has("shrugs off") || Has("luck softens") || Has("resists"))
            return ("🛡", LogCategory.Ward);

        // A life-drain is primarily an attack (it also heals), so classify it before heals.
        if (Has("draining") && Has(" from ")) return ("⚔", LogCategory.PartyHit);

        // Healing and regeneration.
        if (Has("regenerates") || Has("mends") || Has("mending") || Has("healing")
            || Has("restoring") || Has("revives") || Has("reviving") || Has("vigour returns"))
            return ("✚", LogCategory.Heal);

        if (Has("misses")) return ("·", LogCategory.Miss);

        // Damage. "X takes N damage" is always a party member being hurt; "X hits Y for N"
        // / "blasting Y for N" depends on whether X is one of ours.
        if (Has(" takes ") && Has("damage")) return ("✸", LogCategory.EnemyHit);
        if ((Has(" hits ") || Has(" shoots ") || Has("blasting ")) && Has(" for "))
            return ByParty() ? ("⚔", LogCategory.PartyHit) : ("✸", LogCategory.EnemyHit);

        // Status & stat-drain riders.
        if (Has("falls asleep") || Has("poison") || Has("paraly") || Has("withers")
            || Has("drains the life") || Has("lulls"))
            return ("✦", LogCategory.Status);

        // Casting, singing, buffs, summons.
        if (Has("casts") || Has("sings") || Has("invokes") || Has("chants")
            || Has("hurls") || Has("answer the call"))
            return ("✨", LogCategory.Cast);

        return ("", LogCategory.Info);
    }
}

/// <summary>
/// The combat-log colour scheme. The default palette reads naturally; the colourblind palette
/// uses the Okabe–Ito set, which keeps every category distinguishable without red/green pairings
/// (and the per-line icons carry the meaning regardless of colour).
/// </summary>
public static class CombatLogPalette
{
    private static IBrush B(string hex) => new SolidColorBrush(Color.Parse(hex));

    // Natural scheme — reds for harm, greens for healing, etc.
    private static readonly IReadOnlyDictionary<LogCategory, IBrush> Default = new Dictionary<LogCategory, IBrush>
    {
        [LogCategory.Info] = B("#A9D0B0"),
        [LogCategory.PartyHit] = B("#E0A85A"),  // we deal damage (amber)
        [LogCategory.EnemyHit] = B("#D9756A"),  // we take damage (red)
        [LogCategory.Heal] = B("#7FB069"),      // healing / regen (green)
        [LogCategory.Ward] = B("#6FD0E8"),      // wards / saves / resists (cyan)
        [LogCategory.EnemyDown] = B("#B6E86F"), // an enemy is slain (lime)
        [LogCategory.Grave] = B("#C0566B"),     // an ally falls / defeat
        [LogCategory.Miss] = B("#7E879B"),      // misses
        [LogCategory.Status] = B("#C18FE0"),    // sleep / poison / drain (purple)
        [LogCategory.Cast] = B("#9FB4E8"),      // spells, songs, buffs
        [LogCategory.Victory] = B("#E8C56B"),   // victory banner (gold)
        [LogCategory.Enrage] = B("#F2683C"),    // a foe turns berserk (fiery orange)
    };

    // Colourblind-friendly (Okabe–Ito): no red-vs-green confusions; harm is orange/vermilion,
    // healing is bluish-green, wards are sky blue, kills are yellow, ally-loss is reddish-purple.
    private static readonly IReadOnlyDictionary<LogCategory, IBrush> Colorblind = new Dictionary<LogCategory, IBrush>
    {
        [LogCategory.Info] = B("#CCCCCC"),
        [LogCategory.PartyHit] = B("#E69F00"),  // orange
        [LogCategory.EnemyHit] = B("#D55E00"),  // vermilion
        [LogCategory.Heal] = B("#009E73"),      // bluish green
        [LogCategory.Ward] = B("#56B4E9"),      // sky blue
        [LogCategory.EnemyDown] = B("#F0E442"), // yellow
        [LogCategory.Grave] = B("#CC79A7"),     // reddish purple
        [LogCategory.Miss] = B("#999999"),      // grey
        [LogCategory.Status] = B("#0072B2"),    // blue
        [LogCategory.Cast] = B("#56B4E9"),      // sky blue (icon ✨ distinguishes it from wards)
        [LogCategory.Victory] = B("#F0E442"),   // yellow
        [LogCategory.Enrage] = B("#D55E00"),    // vermilion (icon 💢 distinguishes it)
    };

    public static IBrush BrushFor(LogCategory category, bool colorblind)
    {
        var palette = colorblind ? Colorblind : Default;
        return palette.TryGetValue(category, out var brush) ? brush : palette[LogCategory.Info];
    }
}
