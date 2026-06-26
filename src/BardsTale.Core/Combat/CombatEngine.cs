using BardsTale.Core.Characters;
using BardsTale.Core.Items;
using BardsTale.Core.Magic;
using BardsTale.Core.Util;

namespace BardsTale.Core.Combat;

public enum CombatActionType { Attack, Defend, CastSpell, Sing, UseItem, Flee }

public enum CombatOutcome { Ongoing, Victory, Defeat, Fled }

/// <summary>Who, if anyone, gets a free opening round.</summary>
public enum SurpriseState { None, PartySurprised, MonstersSurprised }

/// <summary>An order issued to a single party member for one round.</summary>
public sealed record CombatCommand(
    Character Actor,
    CombatActionType Action,
    int TargetGroup = 0,
    Spell? Spell = null,
    Song? Song = null,
    Item? Item = null,
    int TargetAllyIndex = -1);

/// <summary>The narrated result of resolving one combat round.</summary>
public sealed class CombatRound
{
    public List<string> Log { get; } = new();
    public CombatOutcome Outcome { get; set; } = CombatOutcome.Ongoing;
}

/// <summary>
/// Resolves turn-based combat between the party and an encounter. The caller
/// gathers one command per acting member, then calls <see cref="ExecuteRound"/>.
/// </summary>
public sealed class CombatEngine
{
    private readonly Party _party;
    private readonly Encounter _encounter;
    private readonly IRandomSource _rng;
    private readonly bool _magicSuppressed;
    private readonly HashSet<Character> _defending = new();

    // Party-wide buffs from protection spells; last for the whole encounter.
    private int _partyAttackBonus;
    private int _partyArmorBonus;
    private int _partyExtraAttacks; // Haste: extra swings per round
    private int _partyRegen;        // Aura: HP restored to the party each round

    // Bard songs are SUSTAINED: their bonuses live only for the round they are sung, so a Bard
    // must keep playing to maintain them. These reset every round and are re-applied by ResolveSong.
    private int _songAttackBonus;
    private int _songArmorBonus;
    private int _songRegen;
    private readonly Dictionary<Character, string> _activeSongBy = new(); // bard -> song id currently playing
    private readonly HashSet<Character> _sangThisRound = new();

    // Consumed on the first round: the surprised side sits it out.
    private bool _skipMonstersFirstRound;
    private bool _skipPartyFirstRound;

    // Boss/elite champions that turn berserk once cornered (see MaybeEnrage).
    private readonly HashSet<Monster> _enrageable = new();

    /// <summary>Fraction of max HP at or below which a boss/elite flips into its enrage.</summary>
    private const double EnrageThreshold = 0.35;

    public CombatEngine(Party party, Encounter encounter, IRandomSource rng,
        bool magicSuppressed = false, SurpriseState? surprise = null)
    {
        _party = party;
        _encounter = encounter;
        _rng = rng;
        _magicSuppressed = magicSuppressed;

        // A lair boss (the lead group of a boss fight) and any elite champion can enrage
        // when driven near death; their summoned rabble cannot.
        if (encounter.IsBoss && encounter.Groups.Count > 0)
            foreach (var m in encounter.Groups[0].Monsters) _enrageable.Add(m);
        foreach (var group in encounter.Groups.Where(g => g.Template.IsElite))
            foreach (var m in group.Monsters) _enrageable.Add(m);

        Surprise = surprise ?? RollSurprise();
        if (Surprise == SurpriseState.MonstersSurprised) _skipMonstersFirstRound = true;
        else if (Surprise == SurpriseState.PartySurprised) _skipPartyFirstRound = true;
    }

    /// <summary>True when this fight takes place in an anti-magic zone.</summary>
    public bool MagicSuppressed => _magicSuppressed;

    /// <summary>Who gets the opening free round, if anyone.</summary>
    public SurpriseState Surprise { get; }

    /// <summary>Lucky parties surprise foes more often and are ambushed less.</summary>
    private SurpriseState RollSurprise()
    {
        var living = _party.Members.Where(m => !m.IsDead).ToList();
        var avgLuck = living.Count == 0 ? 10.0 : living.Average(m => m.Attributes.Luck);
        var mod = (avgLuck - 10) * 0.01;
        var ambush = Math.Clamp(0.15 - mod, 0.05, 0.30);
        var surprise = Math.Clamp(0.15 + mod, 0.05, 0.30);

        var roll = _rng.NextDouble();
        if (roll < ambush) return SurpriseState.PartySurprised;
        if (roll < ambush + surprise) return SurpriseState.MonstersSurprised;
        return SurpriseState.None;
    }

    public Encounter Encounter => _encounter;
    public bool IsOver => _encounter.IsCleared || _party.IsWiped;

    public CombatRound ExecuteRound(IReadOnlyList<CombatCommand> commands)
    {
        var round = new CombatRound();
        _defending.Clear();

        // Songs are sustained per round: clear last round's song bonuses; ResolveSong re-applies
        // them only for bards who keep playing this round.
        _songAttackBonus = _songArmorBonus = _songRegen = 0;
        _sangThisRound.Clear();

        // On a surprise round the caught-off-guard side does nothing.
        var skipMonsters = _skipMonstersFirstRound;
        var skipParty = _skipPartyFirstRound;
        _skipMonstersFirstRound = false;
        _skipPartyFirstRound = false;
        if (skipParty)
            commands = System.Array.Empty<CombatCommand>();

        // Ongoing afflictions tick before anyone acts: poison bites, sleepers may
        // rouse, the paralysed may shake free.
        ProcessAfflictions(round);

        // A single flee attempt for the whole party short-circuits the round.
        if (commands.Any(c => c.Action == CombatActionType.Flee))
        {
            if (_rng.Chance(0.5))
            {
                round.Log.Add("The party flees from combat!");
                round.Outcome = CombatOutcome.Fled;
                WakeAll();
                return round;
            }
            round.Log.Add("The party tries to flee, but the way is blocked!");
        }

        foreach (var c in commands.Where(c => c.Action == CombatActionType.Defend))
            _defending.Add(c.Actor);

        // Party-wide preparation resolves first so songs and protective spells
        // shape the whole round, not just the actions after the caster's turn.
        // In an anti-magic zone none of it works.
        var preActed = new HashSet<Character>();
        if (!_magicSuppressed)
        {
            foreach (var c in commands.Where(c => c.Action == CombatActionType.Sing && c.Song is not null && c.Actor.CanAct))
            {
                ResolveSong(c, round);
                preActed.Add(c.Actor);
            }
            foreach (var c in commands.Where(IsBuffSpell))
            {
                ResolveSpell(c, round);
                preActed.Add(c.Actor);
            }
        }

        foreach (var actor in BuildInitiative(commands, preActed, skipMonsters))
            actor(round);

        // A Bard who stopped playing (acted otherwise, fell, or was silenced) lets their song lapse.
        foreach (var bard in _activeSongBy.Keys.ToList())
            if (!_sangThisRound.Contains(bard))
            {
                round.Log.Add($"The strains of {Songs.Get(_activeSongBy[bard]).Name} fade as {bard.Name} falls silent.");
                _activeSongBy.Remove(bard);
            }

        // End-of-round mending: an Aura of Renewal and a sustained Hymn of Renewal heal the party...
        var regen = _partyRegen + _songRegen;
        if (regen > 0 && _party.Members.Any(m => !m.IsDead))
        {
            foreach (var m in _party.Members.Where(m => !m.IsDead))
                m.Heal(regen);
            round.Log.Add($"A restoring aura mends the party for {regen}.");
        }

        // ...and any hero with a regenerative accessory mends a little more, noted when it heals.
        foreach (var m in _party.Members.Where(m => !m.IsDead && m.RegenPerRound > 0 && m.HitPoints < m.EffectiveMaxHitPoints))
        {
            var before = m.HitPoints;
            m.Heal(m.RegenPerRound);
            var healed = m.HitPoints - before;
            if (healed > 0) round.Log.Add($"{m.Name} regenerates {healed} HP.");
        }

        if (_encounter.IsCleared)
        {
            round.Outcome = CombatOutcome.Victory;
            round.Log.Add($"The enemies are defeated! ({_encounter.TotalExperience} XP, {_encounter.TotalGold} gold)");
            WakeAll();
        }
        else if (_party.IsWiped)
        {
            round.Outcome = CombatOutcome.Defeat;
            round.Log.Add("The party has fallen...");
        }

        return round;
    }

    /// <summary>Ticks poison damage and rolls recovery for sleeping/paralysed members.</summary>
    private void ProcessAfflictions(CombatRound round)
    {
        foreach (var m in _party.Members)
        {
            if (m.IsDead) continue;

            if (m.IsPoisoned)
            {
                var dmg = _rng.Roll(1, 3);
                m.ApplyDamage(dmg);
                round.Log.Add($"{m.Name} takes {dmg} poison damage.");
                if (m.IsDead)
                {
                    round.Log.Add($"{m.Name} succumbs to the poison!");
                    continue;
                }
            }

            if (m.IsAsleep && _rng.Chance(0.5))
            {
                m.Wake();
                round.Log.Add($"{m.Name} wakes up.");
            }
            else if (m.IsParalyzed && _rng.Chance(0.25))
            {
                m.Status &= ~StatusEffect.Paralyzed;
                round.Log.Add($"{m.Name} shakes off the paralysis.");
            }
        }
    }

    /// <summary>Sleep ends when the fight does — rouse anyone still slumbering.</summary>
    private void WakeAll()
    {
        foreach (var m in _party.Members)
            m.Wake();
    }

    /// <summary>Applies a monster's special rider (level drain, gold theft) on a landed hit.</summary>
    private void TryMonsterAbility(Monster monster, Character target, CombatRound round)
    {
        var ability = monster.Template.Ability;
        if (ability == MonsterAbility.None || target.IsDead) return;
        if (!_rng.Chance(monster.Template.AbilityChance)) return;

        switch (ability)
        {
            case MonsterAbility.DrainLevel:
                target.DrainLevel();
                round.Log.Add($"{monster.Name} drains the life-force from {target.Name}!");
                break;
            case MonsterAbility.DrainStat:
                var attr = (Characters.Attribute)_rng.Next(0, 5);
                target.DrainAttribute(attr);
                round.Log.Add($"{monster.Name} withers {target.Name}'s {AttributeSet.Abbreviation(attr)}!");
                break;
            case MonsterAbility.StealGold:
                var stolen = Math.Min(_party.Gold, _rng.Roll(2, 20));
                _party.Gold -= stolen;
                round.Log.Add(stolen > 0
                    ? $"{monster.Name} snatches {stolen} gold from the party!"
                    : $"{monster.Name} paws at empty purses.");
                break;
        }
    }

    private void TryInflictStatus(Monster monster, Character target, CombatRound round)
    {
        var status = monster.Template.InflictsStatus;
        if (status == StatusEffect.None || target.IsDead) return;
        if (target.Status.HasFlag(status)) return;
        if (!_rng.Chance(monster.Template.StatusChance)) return;

        if (target.IsImmuneTo(status))
        {
            round.Log.Add($"{target.Name}'s ward shrugs off the {monster.Name}'s {monster.Template.StatusVerb}.");
            return;
        }

        target.Inflict(status);
        round.Log.Add($"{monster.Name} {monster.Template.StatusVerb} {target.Name}!");
    }

    private static bool IsBuffSpell(CombatCommand c)
        => c.Action == CombatActionType.CastSpell && c.Actor.CanAct
           && c.Spell?.Effect is SpellEffect.BuffPartyArmor or SpellEffect.BuffPartyAttack
               or SpellEffect.HasteParty or SpellEffect.RegenParty
               or SpellEffect.CleanseParty or SpellEffect.RestorePartySpellPoints;

    /// <summary>Interleaves party and monster actions by an initiative roll (higher acts first).</summary>
    private List<Action<CombatRound>> BuildInitiative(IReadOnlyList<CombatCommand> commands,
        HashSet<Character> alreadyActed, bool skipMonsters = false)
    {
        var entries = new List<(int init, Action<CombatRound> act)>();

        foreach (var cmd in commands)
        {
            if (cmd.Action is CombatActionType.Flee or CombatActionType.Defend or CombatActionType.Sing) continue;
            if (alreadyActed.Contains(cmd.Actor)) continue;
            if (!cmd.Actor.CanAct) continue;
            var init = cmd.Actor.DexterityBonus + _rng.Next(1, 11);
            entries.Add((init, r => ResolvePartyCommand(cmd, r)));
        }

        if (!skipMonsters)
            foreach (var group in _encounter.Groups)
            {
                foreach (var monster in group.Monsters.Where(m => !m.IsDead))
                {
                    var captured = monster;
                    var init = captured.Template.Speed + _rng.Next(1, 11);
                    entries.Add((init, r => ResolveMonsterTurn(captured, r)));
                }
            }

        return entries
            .OrderByDescending(e => e.init)
            .Select(e => e.act)
            .ToList();
    }

    private void ResolvePartyCommand(CombatCommand cmd, CombatRound round)
    {
        if (cmd.Actor.IsDead) return;
        switch (cmd.Action)
        {
            case CombatActionType.Attack:
                ResolvePartyAttack(cmd, round);
                break;
            case CombatActionType.CastSpell when cmd.Spell is not null:
                if (_magicSuppressed)
                    round.Log.Add($"{cmd.Actor.Name}'s spell sputters out — magic is dead here.");
                else
                    ResolveSpell(cmd, round);
                break;
            case CombatActionType.UseItem when cmd.Item is not null:
                ResolveUseItem(cmd, round);
                break;
        }
    }

    private void ResolveUseItem(CombatCommand cmd, CombatRound round)
    {
        var item = cmd.Item!;
        if (!_party.Inventory.Remove(item))
        {
            round.Log.Add($"{cmd.Actor.Name} reaches for a {item.Name}, but it's gone.");
            return;
        }

        var ally = ResolveAlly(cmd.TargetAllyIndex) ?? cmd.Actor;
        switch (item.Consumable)
        {
            case ConsumableEffect.Heal:
                ally.Heal(item.Power);
                round.Log.Add($"{cmd.Actor.Name} uses a {item.Name} on {ally.Name}, restoring {item.Power} HP.");
                break;
            case ConsumableEffect.RestoreSpellPoints:
                ally.SpellPoints = Math.Min(ally.EffectiveMaxSpellPoints, ally.SpellPoints + item.Power);
                round.Log.Add($"{cmd.Actor.Name} gives {ally.Name} a {item.Name}, restoring spell points.");
                break;
            case ConsumableEffect.Cure:
                ally.CureAilments();
                round.Log.Add($"{cmd.Actor.Name} uses a {item.Name} on {ally.Name}, curing their ailments.");
                break;
            case ConsumableEffect.Revive:
                if (ally.IsDead)
                {
                    ally.Status &= ~StatusEffect.Dead;
                    ally.HitPoints = Math.Max(1, item.Power);
                    round.Log.Add($"{cmd.Actor.Name} sprinkles {item.Name} on {ally.Name}, reviving them!");
                }
                else
                {
                    round.Log.Add($"{cmd.Actor.Name}'s {item.Name} finds no fallen ally.");
                }
                break;
        }
    }

    /// <summary>Scales raw damage by a monster's elemental affinity: ×2 if weak, ÷2 if resistant.</summary>
    private static int ScaleByElement(string monsterName, int dmg, Element element, out string note)
    {
        if (element != Element.None && (MonsterElements.WeakOf(monsterName) & element) != 0)
        {
            note = " — weak, double damage!";
            return dmg * 2;
        }
        if (element != Element.None && (MonsterElements.ResistOf(monsterName) & element) != 0)
        {
            note = " — resisted";
            return Math.Max(1, dmg / 2);
        }
        note = "";
        return dmg;
    }

    private void ResolvePartyAttack(CombatCommand cmd, CombatRound round)
    {
        var group = GetTargetGroup(cmd.TargetGroup);
        if (group is null) return;

        var attacker = cmd.Actor;
        var swings = attacker.AttacksPerRound + _partyExtraAttacks;
        var weapon = attacker.EffectiveWeapon;
        var atkBonus = _partyAttackBonus + _songAttackBonus; // spell buffs plus any sustained war-song
        var attackBonus = attacker.StrengthBonus + (attacker.Level - 1) / 2
            + attacker.Definition.BaseHitBonus + atkBonus + weapon.MagicBonus + attacker.GearHitBonus;

        for (var i = 0; i < swings; i++)
        {
            var target = group.FirstAlive();
            if (target is null) break;

            if (RollToHit(attackBonus, target.ArmorClass))
            {
                var raw = Math.Max(1, _rng.Roll(weapon.DamageDice, weapon.DamageSides,
                    weapon.DamageBonus + attacker.StrengthBonus + atkBonus + attacker.GearDamageBonus));
                var dmg = ScaleByElement(target.Name, raw, Element.Physical, out var note);
                target.HitPoints -= dmg;
                var verb = weapon.Ranged ? "shoots" : "hits";
                round.Log.Add($"{attacker.Name} {verb} {target.Name} for {dmg}{note}.");
                if (target.IsDead)
                    round.Log.Add($"{target.Name} is slain!");
            }
            else
            {
                round.Log.Add(weapon.Ranged
                    ? $"{attacker.Name}'s shot goes wide of {group.Name}."
                    : $"{attacker.Name} misses {group.Name}.");
            }
        }
    }

    private void ResolveSpell(CombatCommand cmd, CombatRound round)
    {
        var caster = cmd.Actor;
        var spell = cmd.Spell!;
        if (caster.SpellPoints < spell.Cost)
        {
            round.Log.Add($"{caster.Name} lacks the spell points for {spell.Name}.");
            return;
        }
        caster.SpellPoints -= spell.Cost;

        switch (spell.Effect)
        {
            case SpellEffect.DamageEnemy:
            {
                var group = GetTargetGroup(cmd.TargetGroup);
                var target = group?.FirstAlive();
                if (target is null) break;
                var dmg = ScaleByElement(target.Name, _rng.Roll(1, spell.Power, spell.Power / 2), spell.Element, out var note);
                target.HitPoints -= dmg;
                round.Log.Add($"{caster.Name} casts {spell.Name}, blasting {target.Name} for {dmg}{note}.");
                if (target.IsDead) round.Log.Add($"{target.Name} is slain!");
                break;
            }
            case SpellEffect.DrainEnemy:
            {
                var group = GetTargetGroup(cmd.TargetGroup);
                var target = group?.FirstAlive();
                if (target is null) break;
                var dmg = ScaleByElement(target.Name, _rng.Roll(1, spell.Power, spell.Power / 2), spell.Element, out var note);
                target.HitPoints -= dmg;
                var healed = Math.Max(1, dmg / 2);
                caster.Heal(healed);
                round.Log.Add($"{caster.Name} casts {spell.Name}, draining {dmg} from {target.Name} and healing {healed}{note}.");
                if (target.IsDead) round.Log.Add($"{target.Name} is slain!");
                break;
            }
            case SpellEffect.DamageAllEnemies:
            {
                round.Log.Add($"{caster.Name} casts {spell.Name}!");
                foreach (var group in _encounter.LivingGroups)
                    foreach (var m in group.Monsters.Where(m => !m.IsDead))
                    {
                        var dmg = ScaleByElement(m.Name, _rng.Roll(1, spell.Power, spell.Power / 2), spell.Element, out _);
                        m.HitPoints -= dmg;
                        if (m.IsDead) round.Log.Add($"{m.Name} is slain!");
                    }
                break;
            }
            case SpellEffect.HealAlly:
            {
                var ally = ResolveAlly(cmd.TargetAllyIndex) ?? caster;
                var heal = _rng.Roll(1, spell.Power, spell.Power / 2);
                ally.Heal(heal);
                round.Log.Add($"{caster.Name} casts {spell.Name}, healing {ally.Name} for {heal}.");
                break;
            }
            case SpellEffect.HealParty:
            {
                round.Log.Add($"{caster.Name} casts {spell.Name}.");
                foreach (var m in _party.Members.Where(m => !m.IsDead))
                    m.Heal(spell.Power);
                break;
            }
            case SpellEffect.Revive:
            {
                var ally = ResolveAlly(cmd.TargetAllyIndex);
                if (ally is { IsDead: true })
                {
                    ally.Status &= ~StatusEffect.Dead;
                    ally.HitPoints = spell.Power;
                    round.Log.Add($"{caster.Name} revives {ally.Name}!");
                }
                else
                {
                    round.Log.Add($"{caster.Name}'s {spell.Name} finds no fallen ally.");
                }
                break;
            }
            case SpellEffect.CureStatus:
            {
                var ally = ResolveAlly(cmd.TargetAllyIndex) ?? caster;
                ally.CureAilments();
                round.Log.Add($"{caster.Name} casts {spell.Name}, cleansing {ally.Name}.");
                break;
            }
            case SpellEffect.BuffPartyArmor:
                _partyArmorBonus = Math.Max(_partyArmorBonus, spell.Power);
                round.Log.Add($"{caster.Name} casts {spell.Name}; the party is harder to hit.");
                break;
            case SpellEffect.BuffPartyAttack:
                _partyAttackBonus = Math.Max(_partyAttackBonus, spell.Power);
                round.Log.Add($"{caster.Name} casts {spell.Name}; the party strikes with renewed force.");
                break;
            case SpellEffect.HasteParty:
                _partyExtraAttacks = Math.Max(_partyExtraAttacks, spell.Power);
                round.Log.Add($"{caster.Name} invokes {spell.Name}; the party blurs into a flurry of blows!");
                break;
            case SpellEffect.RegenParty:
                _partyRegen = Math.Max(_partyRegen, spell.Power);
                round.Log.Add($"{caster.Name} invokes {spell.Name}; a healing aura wraps the party.");
                break;
            case SpellEffect.CleanseParty:
                foreach (var m in _party.Members.Where(m => !m.IsDead))
                    m.CureAilments();
                round.Log.Add($"{caster.Name} invokes {spell.Name}; ailments are washed away.");
                break;
            case SpellEffect.RestorePartySpellPoints:
                foreach (var m in _party.Members.Where(m => !m.IsDead))
                    m.SpellPoints = Math.Min(m.EffectiveMaxSpellPoints, m.SpellPoints + spell.Power);
                round.Log.Add($"{caster.Name} invokes {spell.Name}; arcane vigour returns to the party.");
                break;
            case SpellEffect.RestoreLight:
                round.Log.Add($"{caster.Name} casts {spell.Name}; light floods the area.");
                break;
        }
    }

    private void ResolveSong(CombatCommand cmd, CombatRound round)
    {
        var bard = cmd.Actor;
        var song = cmd.Song!;

        // Striking up a tune the bard wasn't already playing spends one of their daily tunes;
        // sustaining the same song round-to-round is free.
        var sustaining = _activeSongBy.TryGetValue(bard, out var current) && current == song.Id;
        if (!sustaining)
        {
            if (bard.BardTunes <= 0)
            {
                round.Log.Add($"{bard.Name}'s voice is spent — no tunes left to strike up {song.Name}.");
                return;
            }
            bard.BardTunes--;
        }
        _activeSongBy[bard] = song.Id;
        _sangThisRound.Add(bard);

        var verb = sustaining ? "sustains" : "strikes up";
        switch (song.Effect)
        {
            case SongEffect.BuffPartyArmor:
                _songArmorBonus = Math.Max(_songArmorBonus, song.Power);
                round.Log.Add($"{bard.Name} {verb} {song.Name}; the party fights warded.");
                break;
            case SongEffect.BuffPartyAttack:
                _songAttackBonus = Math.Max(_songAttackBonus, song.Power);
                round.Log.Add($"{bard.Name} {verb} {song.Name}; the party fights emboldened.");
                break;
            case SongEffect.RegenParty:
                _songRegen = Math.Max(_songRegen, song.Power);
                round.Log.Add($"{bard.Name} {verb} {song.Name}; a mending refrain wraps the party.");
                break;
            case SongEffect.HealParty:
                round.Log.Add($"{bard.Name} {verb} {song.Name}.");
                foreach (var m in _party.Members.Where(m => !m.IsDead))
                    m.Heal(song.Power);
                break;
            case SongEffect.Light:
                round.Log.Add($"{bard.Name} {verb} {song.Name}; light fills the hall.");
                break;
        }
    }

    /// <summary>A monster either casts its signature spell (if useful) or attacks.</summary>
    private void ResolveMonsterTurn(Monster monster, CombatRound round)
    {
        if (monster.IsDead) return;

        if (TryRout(monster, round)) return;
        MaybeEnrage(monster, round);

        var spell = monster.Template.Spell;
        if (!_magicSuppressed && spell is not null && CanCastUsefully(spell)
            && _rng.Chance(EffectiveCastChance(monster, spell)))
        {
            CastMonsterSpell(monster, spell, round);
            return;
        }
        ResolveMonsterAttack(monster, round);
    }

    /// <summary>
    /// Drives a boss or elite berserk the first time it is cornered below the enrage
    /// threshold. From then on <see cref="Monster.Enraged"/> sharpens its attacks and casting.
    /// </summary>
    private void MaybeEnrage(Monster monster, CombatRound round)
    {
        if (monster.Enraged || !_enrageable.Contains(monster)) return;
        if (monster.HitPoints > monster.Template.MaxHitPoints * EnrageThreshold) return;

        monster.Enraged = true;
        round.Log.Add($"{monster.Name} ROARS in fury — its wounds drive it into a frenzy!");
    }

    /// <summary>
    /// Morale check: a cornered rank-and-file monster may break and flee the field once the tide
    /// has clearly turned — it is badly wounded and its side is outnumbered or its group all but
    /// wiped. Bosses, elites (which enrage instead) and the toughest brutes never lose their nerve.
    /// A routed monster leaves entirely: no XP, no gold.
    /// </summary>
    private bool TryRout(Monster monster, CombatRound round)
    {
        if (_enrageable.Contains(monster)) return false;                       // bosses & elites hold
        if (monster.Template.MaxHitPoints >= 45) return false;                 // big brutes are fearless
        if (monster.HitPoints > monster.Template.MaxHitPoints * 0.35) return false; // not yet desperate

        var livingMonsters = _encounter.Groups.Sum(g => g.LivingCount);
        var outnumbered = livingMonsters < _party.LivingCount;
        var group = _encounter.Groups.FirstOrDefault(g => g.Monsters.Contains(monster));
        var lastOfGroup = group is not null && group.LivingCount == 1;
        if (!outnumbered && !lastOfGroup) return false;                        // only break when losing

        var chance = 0.25
            + (lastOfGroup ? 0.15 : 0.0)
            + (outnumbered ? 0.15 : 0.0)
            + Math.Max(0, 14 - monster.Template.MaxHitPoints) * 0.01;          // weaker creatures break sooner
        if (!_rng.Chance(Math.Min(0.6, chance))) return false;

        group?.Remove(monster);
        round.Log.Add($"{monster.Name} loses its nerve and flees the battle!");
        return true;
    }

    private bool CanCastUsefully(MonsterSpell spell) => spell.Kind switch
    {
        MonsterSpellKind.SleepFoe => _party.Members.Any(m => !m.IsDead && !m.IsAsleep),
        MonsterSpellKind.HealAllies => _encounter.Groups.SelectMany(g => g.Monsters).Any(m => m.IsWounded),
        MonsterSpellKind.BlastParty => _party.LivingCount > 0,
        MonsterSpellKind.DamageFoe => _party.LivingCount > 0,
        MonsterSpellKind.Summon => spell.SummonTemplate is not null && _encounter.CanSummonMore,
        _ => false
    };

    /// <summary>
    /// A smarter caster reads the battlefield: it leans on healing when an ally is dying,
    /// favours area blasts against a clustered party, and presses spells harder once enraged.
    /// </summary>
    private double EffectiveCastChance(Monster monster, MonsterSpell spell)
    {
        var chance = spell.Chance;
        switch (spell.Kind)
        {
            case MonsterSpellKind.HealAllies:
                // Triage: cast far more readily when an ally is gravely wounded.
                if (_encounter.Groups.SelectMany(g => g.Monsters)
                        .Any(m => !m.IsDead && m.HitPoints <= m.Template.MaxHitPoints * 0.35))
                    chance = Math.Max(chance, 0.85);
                break;
            case MonsterSpellKind.BlastParty:
                // Area spells earn their cost against a full party.
                if (_party.LivingCount >= 4) chance += 0.15;
                break;
            case MonsterSpellKind.SleepFoe:
                // Worth opening with while the party is still awake and intact.
                if (_party.Members.Count(m => !m.IsDead && !m.IsAsleep) >= 4) chance += 0.10;
                break;
        }

        if (monster.Enraged) chance *= 1.4;
        return Math.Clamp(chance, 0.0, 0.95);
    }

    private void CastMonsterSpell(Monster caster, MonsterSpell spell, CombatRound round)
    {
        switch (spell.Kind)
        {
            case MonsterSpellKind.SleepFoe:
            {
                round.Log.Add($"{caster.Name} casts {spell.Name}!");
                var targets = _party.Members
                    .Where(m => !m.IsDead && !m.IsAsleep)
                    .OrderBy(_ => _rng.Next(0, 1000))
                    .Take(Math.Max(1, spell.Power))
                    .ToList();
                foreach (var t in targets)
                {
                    if (t.IsImmuneTo(StatusEffect.Asleep) || ResistsSleep(t))
                        round.Log.Add($"{t.Name} resists the slumber.");
                    else
                    {
                        t.Inflict(StatusEffect.Asleep);
                        round.Log.Add($"{t.Name} falls asleep!");
                    }
                }
                break;
            }
            case MonsterSpellKind.HealAllies:
            {
                var wounded = _encounter.Groups
                    .SelectMany(g => g.Monsters)
                    .Where(m => m.IsWounded)
                    .OrderBy(m => m.HitPoints)
                    .FirstOrDefault();
                if (wounded is null)
                {
                    ResolveMonsterAttack(caster, round);
                    break;
                }
                wounded.Heal(spell.Power);
                round.Log.Add($"{caster.Name} chants {spell.Name}, mending {wounded.Name}.");
                break;
            }
            case MonsterSpellKind.BlastParty:
            {
                round.Log.Add($"{caster.Name} casts {spell.Name}, blasting the party!");
                foreach (var m in _party.Members.Where(m => !m.IsDead).ToList())
                {
                    var dmg = _rng.Roll(1, spell.Power);
                    HitMemberWithSpell(m, dmg, spell.Element, round, luckySave: LuckySave(m));
                }
                break;
            }
            case MonsterSpellKind.DamageFoe:
            {
                var target = PickPartyTarget();
                if (target is null) break;
                var dmg = _rng.Roll(1, spell.Power, spell.Power / 2);
                round.Log.Add($"{caster.Name} hurls {spell.Name} at {target.Name}!");
                HitMemberWithSpell(target, dmg, spell.Element, round);
                break;
            }
            case MonsterSpellKind.Summon:
            {
                if (spell.SummonTemplate is null) break;
                var count = Math.Max(1, spell.Power);
                if (_encounter.AddReinforcements(spell.SummonTemplate, count) is not null)
                    round.Log.Add($"{caster.Name} casts {spell.Name} — {count} {spell.SummonTemplate.Name} answer the call!");
                else
                    ResolveMonsterAttack(caster, round);
                break;
            }
        }
    }

    /// <summary>Applies elemental spell damage to a party member; luck and warded gear each halve it (and are noted).</summary>
    private void HitMemberWithSpell(Character target, int dmg, Element element, CombatRound round, bool luckySave = false)
    {
        var notes = new List<string>();
        if (luckySave)
        {
            dmg = Math.Max(1, dmg / 2);
            notes.Add("luck softens it");
        }
        var warded = target.Resists(element);
        if (warded)
        {
            dmg = Math.Max(1, dmg / 2);
            notes.Add($"warded against {element.ToString().ToLowerInvariant()}");
        }
        var wasAsleep = target.IsAsleep;
        target.ApplyDamage(dmg);
        var suffix = notes.Count > 0 ? $" — {string.Join(", ", notes)}" : "";
        round.Log.Add($"{target.Name} takes {dmg} damage{suffix}.");
        if (target.IsDead)
            round.Log.Add($"{target.Name} has fallen!");
        else if (wasAsleep)
        {
            target.Wake();
            round.Log.Add($"{target.Name} is jolted awake!");
        }
    }

    /// <summary>A luckier hero may shrug off half of an area blast.</summary>
    private bool LuckySave(Character c) => _rng.Next(1, 21) <= c.EffectiveLuck / 3;

    /// <summary>A luckier hero is more likely to shrug off an enemy sleep spell.</summary>
    private bool ResistsSleep(Character c) => _rng.Next(1, 21) <= c.EffectiveLuck / 2;

    private void ResolveMonsterAttack(Monster monster, CombatRound round)
    {
        if (monster.IsDead) return;

        // An enraged boss/elite lashes out twice and hits harder; everyone else swings once.
        var swings = monster.Enraged ? 2 : 1;
        var hitBonus = monster.Enraged ? 2 : 0;
        var damageBonus = monster.Enraged ? 2 : 0;

        for (var i = 0; i < swings; i++)
        {
            if (_party.LivingCount == 0) return;
            ResolveMonsterSwing(monster, hitBonus, damageBonus, round);
        }
    }

    /// <summary>A single attack: pick a target, roll to hit, and apply damage and riders.</summary>
    private void ResolveMonsterSwing(Monster monster, int hitBonus, int damageBonus, CombatRound round)
    {
        var target = PickPartyTarget();
        if (target is null) return;

        var ac = target.ArmorClass - (_partyArmorBonus + _songArmorBonus) + (_defending.Contains(target) ? -2 : 0);
        if (RollToHit(monster.Template.AttackBonus + hitBonus, ac))
        {
            var dmg = _rng.Roll(monster.Template.AttackDice, monster.Template.AttackSides,
                monster.Template.AttackBonus + damageBonus);
            var wasAsleep = target.IsAsleep;
            target.ApplyDamage(dmg);
            round.Log.Add($"{monster.Name} hits {target.Name} for {dmg}.");

            if (target.IsDead)
            {
                round.Log.Add($"{target.Name} has fallen!");
            }
            else
            {
                if (wasAsleep)
                {
                    target.Wake();
                    round.Log.Add($"{target.Name} is jolted awake!");
                }
                else
                {
                    TryInflictStatus(monster, target, round);
                }
                TryMonsterAbility(monster, target, round);
            }
        }
        else
        {
            round.Log.Add($"{monster.Name} misses {target.Name}.");
        }
    }

    /// <summary>d20 + bonus beats a threshold derived from target armour class (lower AC is harder to hit).</summary>
    private bool RollToHit(int attackBonus, int targetArmorClass)
        => _rng.Next(1, 21) + attackBonus + targetArmorClass >= 20;

    private MonsterGroup? GetTargetGroup(int index)
    {
        if (index >= 0 && index < _encounter.Groups.Count && !_encounter.Groups[index].IsDefeated)
            return _encounter.Groups[index];
        return _encounter.LivingGroups.FirstOrDefault();
    }

    private Character? ResolveAlly(int index) => _party[index];

    /// <summary>
    /// Monsters strike the front rank (first three living members), but a cannier foe
    /// focuses fire: most of the time it goes for the weakest hero still standing — the
    /// one closest to death — to press its advantage; otherwise it lashes out at random.
    /// </summary>
    private Character? PickPartyTarget()
    {
        var front = _party.FrontRank.Where(m => !m.IsDead).ToList();
        if (front.Count == 0) return null;

        if (_rng.Chance(0.6))
        {
            var weakest = front
                .OrderBy(m => m.HitPoints)
                .ThenBy(m => m.EffectiveMaxHitPoints)
                .First();
            return weakest;
        }

        return front[_rng.Next(0, front.Count)];
    }
}
