using System.Collections.Generic;
using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Magic;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Covers monster-side status effects: party spells that sleep or poison enemy groups, the
/// per-round tick, waking a slept foe by striking it, and tough monsters resisting control.
/// </summary>
public class MonsterStatusTests
{
    // A lone, durable mage that knows everything — so we can cast freely and never die.
    private static Party MageParty(out Character mage)
    {
        var factory = new CharacterFactory(new SystemRandomSource(seed: 5));
        mage = factory.Create("Vex", Race.Gnome, CharacterClass.Sorcerer);
        mage.MaxHitPoints = 9999; mage.HitPoints = 9999;
        mage.MaxSpellPoints = 999; mage.SpellPoints = 999;
        foreach (var id in new[] { "MIFO", "VESP" })
            if (!mage.KnownSpells.Contains(id)) mage.KnownSpells.Add(id);
        var party = new Party();
        party.Add(mage);
        return party;
    }

    private static CombatRound Cast(CombatEngine engine, Character mage, string spellId)
        => engine.ExecuteRound(new[] { new CombatCommand(mage, CombatActionType.CastSpell, 0, Spell: Spells.Get(spellId)) });

    [Fact]
    public void The_new_control_spells_exist_and_target_enemies()
    {
        var fog = Spells.Get("MIFO");
        var venom = Spells.Get("VESP");
        Assert.Equal(SpellEffect.SleepEnemies, fog.Effect);
        Assert.Equal(SpellEffect.PoisonEnemies, venom.Effect);
        Assert.True(fog.TargetsEnemies && venom.TargetsEnemies);
        Assert.True(fog.UsableInCombat && venom.UsableInCombat);
    }

    [Fact]
    public void Venom_spray_poisons_a_group_and_it_bleeds_each_round()
    {
        var party = MageParty(out var mage);
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Ogre, 2) }); // tanky, won't die instantly
        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 3), surprise: SurpriseState.MonstersSurprised);

        Cast(engine, mage, "VESP");
        Assert.All(encounter.Groups[0].Monsters, m => Assert.True(m.IsPoisoned));

        var hpBefore = encounter.Groups[0].Monsters.Sum(m => m.HitPoints);
        engine.ExecuteRound(new[] { new CombatCommand(mage, CombatActionType.Defend) }); // poison ticks at round start
        Assert.True(encounter.Groups[0].Monsters.Sum(m => m.HitPoints) < hpBefore, "poison should sap HP each round");
    }

    [Fact]
    public void A_sleeping_monster_skips_its_turn_then_a_blow_wakes_it()
    {
        var party = MageParty(out var mage);
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Goblin, 1) });
        var monster = encounter.Groups[0].Monsters[0];
        monster.Sleep(2);
        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 1), surprise: SurpriseState.None);

        // It dozes through a round while the party defends — no attack from it.
        var dozed = engine.ExecuteRound(new[] { new CombatCommand(mage, CombatActionType.Defend) });
        Assert.DoesNotContain(dozed.Log, l => l.StartsWith("Goblin "));
        Assert.True(monster.IsAsleep);

        // A blow jolts it awake.
        mage.Weapon = BardsTale.Core.Items.Items.LongSword;
        mage.Attributes.Strength = 18;
        var struck = engine.ExecuteRound(new[] { new CombatCommand(mage, CombatActionType.Attack, 0) });
        Assert.False(monster.IsDead && monster.IsAsleep); // either slain or awake — never a struck sleeper
        if (!monster.IsDead) Assert.False(monster.IsAsleep);
    }

    [Fact]
    public void Mind_fog_usually_fells_a_weak_group()
    {
        var slept = 0;
        for (var seed = 0; seed < 30; seed++)
        {
            var party = MageParty(out var mage);
            var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) });
            var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed), surprise: SurpriseState.MonstersSurprised);
            Cast(engine, mage, "MIFO");
            if (encounter.Groups[0].Monsters[0].IsAsleep) slept++;
        }
        Assert.True(slept > 20, $"a weak rat should usually be lulled; slept {slept}/30");
    }

    [Fact]
    public void Tough_monsters_resist_sleep_far_more_than_weak_ones()
    {
        int SleptCount(MonsterTemplate t)
        {
            var n = 0;
            for (var seed = 0; seed < 30; seed++)
            {
                var party = MageParty(out var mage);
                var encounter = new Encounter(new[] { new MonsterGroup(t, 1) });
                var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed), surprise: SurpriseState.MonstersSurprised);
                Cast(engine, mage, "MIFO");
                if (encounter.Groups[0].Monsters[0].IsAsleep) n++;
            }
            return n;
        }

        Assert.True(SleptCount(Bestiary.GiantRat) > SleptCount(Bestiary.Tarrasque),
            "a mighty Tarrasque should shrug off slumber far more often than a rat");
    }
}
