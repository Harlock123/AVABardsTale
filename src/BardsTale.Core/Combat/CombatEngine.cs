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

    // Party-wide buffs from songs and protection spells; last for the whole encounter.
    private int _partyAttackBonus;
    private int _partyArmorBonus;
    private int _partyExtraAttacks; // Haste: extra swings per round
    private int _partyRegen;        // Aura: HP restored to the party each round

    // Consumed on the first round: the surprised side sits it out.
    private bool _skipMonstersFirstRound;
    private bool _skipPartyFirstRound;

    public CombatEngine(Party party, Encounter encounter, IRandomSource rng,
        bool magicSuppressed = false, SurpriseState? surprise = null)
    {
        _party = party;
        _encounter = encounter;
        _rng = rng;
        _magicSuppressed = magicSuppressed;

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

        // End-of-round mending: an Aura of Renewal heals the whole party...
        if (_partyRegen > 0 && _party.Members.Any(m => !m.IsDead))
        {
            foreach (var m in _party.Members.Where(m => !m.IsDead))
                m.Heal(_partyRegen);
            round.Log.Add($"A restoring aura mends the party for {_partyRegen}.");
        }

        // ...and any hero with a regenerative accessory mends a little more, noted when it heals.
        foreach (var m in _party.Members.Where(m => !m.IsDead && m.RegenPerRound > 0 && m.HitPoints < m.MaxHitPoints))
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
                ally.SpellPoints = Math.Min(ally.MaxSpellPoints, ally.SpellPoints + item.Power);
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
        var attackBonus = attacker.StrengthBonus + (attacker.Level - 1) / 2
            + attacker.Definition.BaseHitBonus + _partyAttackBonus + weapon.MagicBonus + attacker.GearHitBonus;

        for (var i = 0; i < swings; i++)
        {
            var target = group.FirstAlive();
            if (target is null) break;

            if (RollToHit(attackBonus, target.ArmorClass))
            {
                var raw = Math.Max(1, _rng.Roll(weapon.DamageDice, weapon.DamageSides,
                    weapon.DamageBonus + attacker.StrengthBonus + _partyAttackBonus + attacker.GearDamageBonus));
                var dmg = ScaleByElement(target.Name, raw, Element.Physical, out var note);
                target.HitPoints -= dmg;
                round.Log.Add($"{attacker.Name} hits {target.Name} for {dmg}{note}.");
                if (target.IsDead)
                    round.Log.Add($"{target.Name} is slain!");
            }
            else
            {
                round.Log.Add($"{attacker.Name} misses {group.Name}.");
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
                    m.SpellPoints = Math.Min(m.MaxSpellPoints, m.SpellPoints + spell.Power);
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
        switch (song.Effect)
        {
            case SongEffect.BuffPartyArmor:
                _partyArmorBonus = Math.Max(_partyArmorBonus, song.Power);
                round.Log.Add($"{bard.Name} sings {song.Name}; the party fights warded.");
                break;
            case SongEffect.BuffPartyAttack:
                _partyAttackBonus = Math.Max(_partyAttackBonus, song.Power);
                round.Log.Add($"{bard.Name} sings {song.Name}; the party fights emboldened.");
                break;
            case SongEffect.HealParty:
                round.Log.Add($"{bard.Name} sings {song.Name}.");
                foreach (var m in _party.Members.Where(m => !m.IsDead))
                    m.Heal(song.Power);
                break;
            case SongEffect.Light:
                round.Log.Add($"{bard.Name} sings {song.Name}; light fills the hall.");
                break;
        }
    }

    /// <summary>A monster either casts its signature spell (if useful) or attacks.</summary>
    private void ResolveMonsterTurn(Monster monster, CombatRound round)
    {
        if (monster.IsDead) return;

        var spell = monster.Template.Spell;
        if (!_magicSuppressed && spell is not null && _rng.Chance(spell.Chance) && CanCastUsefully(spell))
        {
            CastMonsterSpell(monster, spell, round);
            return;
        }
        ResolveMonsterAttack(monster, round);
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
        var target = PickPartyTarget();
        if (target is null) return;

        var ac = target.ArmorClass - _partyArmorBonus + (_defending.Contains(target) ? -2 : 0);
        if (RollToHit(monster.Template.AttackBonus, ac))
        {
            var dmg = _rng.Roll(monster.Template.AttackDice, monster.Template.AttackSides, monster.Template.AttackBonus);
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

    /// <summary>Monsters strike the front rank (first three living members) preferentially.</summary>
    private Character? PickPartyTarget()
    {
        var front = _party.FrontRank.ToList();
        if (front.Count == 0) return null;
        return front[_rng.Next(0, front.Count)];
    }
}
