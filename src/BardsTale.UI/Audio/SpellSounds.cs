using BardsTale.Core.Magic;

namespace BardsTale.UI.Audio;

/// <summary>Picks a sound effect to match a spell's effect, so a blast sounds nothing like a heal.</summary>
public static class SpellSounds
{
    public static GameSound For(SpellEffect effect) => effect switch
    {
        SpellEffect.DamageEnemy or SpellEffect.DamageAllEnemies => GameSound.SpellFire,
        SpellEffect.HealAlly or SpellEffect.HealParty or SpellEffect.Revive or SpellEffect.CureStatus => GameSound.Heal,
        SpellEffect.BuffPartyArmor or SpellEffect.BuffPartyAttack or SpellEffect.RestoreLight
            or SpellEffect.Identify => GameSound.SpellBuff,
        _ => GameSound.SpellCast
    };
}
