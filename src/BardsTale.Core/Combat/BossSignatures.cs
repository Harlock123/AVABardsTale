using System;
using System.Collections.Generic;
using BardsTale.Core.Characters;

namespace BardsTale.Core.Combat;

/// <summary>How a telegraphed signature attack resolves when it finally lands.</summary>
public enum SignaturePayload
{
    /// <summary>Catches the whole party, softened by bracing (Defend) and luck.</summary>
    PartyBlast,
    /// <summary>A single overwhelming bolt at one hero — far harder to survive head-on.</summary>
    FocusedBolt
}

/// <summary>
/// A boss's named signature attack — the devastating move it winds up (telegraphs) once driven
/// into its second phase. Marquee bosses get bespoke ones with riders or self-healing; any other
/// damaging-spell boss falls back to an amplified version of its ordinary cast.
/// </summary>
public sealed record BossSignature(
    string Name,
    Element Element,
    SignaturePayload Payload,
    int Power,
    StatusEffect Rider = StatusEffect.None,
    double RiderChance = 0.0,
    bool HealsSelf = false);

/// <summary>The catalogue of bespoke boss signatures, looked up by the boss's base name.</summary>
public static class BossSignatures
{
    private static readonly Dictionary<string, BossSignature> Catalogue = new()
    {
        ["Demon Lord"] = new("Hellfire Cataclysm", Element.Fire, SignaturePayload.PartyBlast, 24),
        ["Frost King"] = new("Killing Winter", Element.Cold, SignaturePayload.PartyBlast, 34,
            Rider: StatusEffect.Paralyzed, RiderChance: 0.30),
        ["Pit Lord"] = new("Apocalypse", Element.Fire, SignaturePayload.PartyBlast, 44),
        ["Dragon Tyrant"] = new("Cataclysm Breath", Element.Fire, SignaturePayload.PartyBlast, 50),
        ["Mangar the Mad"] = new("Mind Storm", Element.Arcane, SignaturePayload.PartyBlast, 44,
            Rider: StatusEffect.Asleep, RiderChance: 0.25),
        ["Beholder Tyrant"] = new("Disintegration Ray", Element.Arcane, SignaturePayload.FocusedBolt, 55),
        ["Archlich"] = new("Soul Harvest", Element.Arcane, SignaturePayload.FocusedBolt, 60, HealsSelf: true),
        ["The Gloomlord"] = new("Gloom Singularity", Element.Arcane, SignaturePayload.PartyBlast, 46,
            Rider: StatusEffect.Asleep, RiderChance: 0.25),
    };

    /// <summary>
    /// The signature a boss telegraphs, or null if it has none. A bespoke entry wins; otherwise any
    /// boss with a damaging spell telegraphs an amplified (~1.7×) version of it.
    /// </summary>
    public static BossSignature? For(MonsterTemplate t)
    {
        if (Catalogue.TryGetValue(t.BaseName, out var bespoke)) return bespoke;
        if (t.Spell is { Kind: MonsterSpellKind.BlastParty or MonsterSpellKind.DamageFoe } s)
            return new BossSignature(
                s.Name, s.Element,
                s.Kind == MonsterSpellKind.BlastParty ? SignaturePayload.PartyBlast : SignaturePayload.FocusedBolt,
                (int)Math.Round(s.Power * 1.7));
        return null;
    }
}
