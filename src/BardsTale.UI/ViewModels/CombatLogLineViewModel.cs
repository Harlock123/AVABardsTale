using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;

namespace BardsTale.UI.ViewModels;

/// <summary>
/// One line of the combat log, classified by what happened so the view can colour and
/// icon it — party hits, blows taken, heals/regen, wards & saves, kills, deaths, misses
/// and casts each read distinctly. Classification is keyword-based; the one ambiguous
/// case ("X hits Y for N") is resolved by checking whether X is a party member.
/// </summary>
public sealed class CombatLogLineViewModel
{
    public CombatLogLineViewModel(string text, ICollection<string> partyNames)
    {
        Text = text;
        (Icon, Brush) = Classify(text, partyNames);
    }

    public string Text { get; }
    public string Icon { get; }
    public IBrush Brush { get; }

    public bool HasIcon => Icon.Length > 0;

    private static (string Icon, IBrush Brush) Classify(string line, ICollection<string> partyNames)
    {
        bool Has(string s) => line.Contains(s, StringComparison.OrdinalIgnoreCase);
        bool ByParty() => partyNames.Any(n => n.Length > 0 && line.StartsWith(n, StringComparison.Ordinal));

        // Deaths — an enemy felled reads as a win, an ally lost reads as a loss.
        if (Has("is slain")) return ("☠", EnemyDown);
        if (Has("has fallen") || Has("succumbs")) return ("⚰", Grave);

        // Victory / defeat banners.
        if (Has("are defeated")) return ("🏆", Victory);

        // Wards, saves and resists — cyan, the standout the player wants to notice.
        if (Has("warded") || Has("shrugs off") || Has("luck softens") || Has("resists"))
            return ("🛡", Cyan);

        // A life-drain is primarily an attack (it also heals), so classify it before heals.
        if (Has("draining") && Has(" from ")) return ("⚔", PartyHit);

        // Healing and regeneration — green.
        if (Has("regenerates") || Has("mends") || Has("mending") || Has("healing")
            || Has("restoring") || Has("revives") || Has("reviving") || Has("vigour returns"))
            return ("✚", Heal);

        if (Has("misses")) return ("·", Faint);

        // Damage. "X takes N damage" is always a party member being hurt; "X hits Y for N"
        // / "blasting Y for N" depends on whether X is one of ours.
        if (Has(" takes ") && Has("damage")) return ("✸", EnemyHit);
        if ((Has(" hits ") || Has("blasting ")) && Has(" for "))
            return ByParty() ? ("⚔", PartyHit) : ("✸", EnemyHit);

        // Status & stat-drain riders.
        if (Has("falls asleep") || Has("poison") || Has("paraly") || Has("withers")
            || Has("drains the life") || Has("lulls"))
            return ("✦", Status);

        // Casting, singing, buffs, summons.
        if (Has("casts") || Has("sings") || Has("invokes") || Has("chants")
            || Has("hurls") || Has("answer the call"))
            return ("✨", Cast);

        return ("", Info);
    }

    private static IBrush B(string hex) => new SolidColorBrush(Color.Parse(hex));

    private static readonly IBrush Info = B("#A9D0B0");      // default narration
    private static readonly IBrush PartyHit = B("#E0A85A");  // we deal damage (amber)
    private static readonly IBrush EnemyHit = B("#D9756A");  // we take damage (red)
    private static readonly IBrush Heal = B("#7FB069");      // healing / regen (green)
    private static readonly IBrush Cyan = B("#6FD0E8");      // wards / saves / resists
    private static readonly IBrush EnemyDown = B("#B6E86F"); // an enemy is slain (lime)
    private static readonly IBrush Grave = B("#C0566B");     // an ally falls / defeat
    private static readonly IBrush Faint = B("#7E879B");     // misses
    private static readonly IBrush Status = B("#C18FE0");    // sleep / poison / drain (purple)
    private static readonly IBrush Cast = B("#9FB4E8");      // spells, songs, buffs
    private static readonly IBrush Victory = B("#E8C56B");   // victory banner (gold)
}
