using System.Collections.Generic;
using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Covers the boss/elite "enrage" mechanic and the smarter monster targeting AI:
/// cornered champions turn berserk (striking twice and harder), and ordinary
/// monsters focus fire on the weakest hero more often than blind chance would.
/// </summary>
public class EnrageAiTests
{
    private static Party MakeDefenders(int count, int hp = 100)
    {
        var factory = new CharacterFactory(new SystemRandomSource(seed: 7));
        var party = new Party();
        for (var i = 0; i < count; i++)
        {
            var c = factory.Create($"Guard{i}", Race.Dwarf, CharacterClass.Warrior);
            c.MaxHitPoints = hp;
            c.HitPoints = hp;
            party.Add(c);
        }
        return party;
    }

    private static IReadOnlyList<string> RunOneRound(CombatEngine engine, Party party)
        => engine.ExecuteRound(party.Members
            .Select(m => new CombatCommand(m, CombatActionType.Defend))
            .ToList()).Log;

    private static int BossSwingLines(IEnumerable<string> log, string name)
        => log.Count(l => l.StartsWith(name) && (l.Contains(" hits ") || l.Contains(" misses ")));

    [Fact]
    public void A_boss_enrages_when_driven_below_the_threshold()
    {
        var party = MakeDefenders(2);
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Ogre, 1) }, isBoss: true);
        var boss = encounter.Groups[0].Monsters[0];
        boss.HitPoints = 9; // below 35% of Ogre's 28 max

        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 3),
            surprise: SurpriseState.None);
        var log = RunOneRound(engine, party);

        Assert.True(boss.Enraged, "a cornered boss should turn berserk");
        Assert.Contains(log, l => l.Contains("fury"));
    }

    [Fact]
    public void A_healthy_boss_does_not_enrage()
    {
        var party = MakeDefenders(2);
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Ogre, 1) }, isBoss: true);
        var boss = encounter.Groups[0].Monsters[0]; // starts at full HP

        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 3),
            surprise: SurpriseState.None);
        RunOneRound(engine, party);

        Assert.False(boss.Enraged);
    }

    [Fact]
    public void An_enraged_boss_strikes_twice_in_a_round()
    {
        var party = MakeDefenders(2);
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Ogre, 1) }, isBoss: true);
        var boss = encounter.Groups[0].Monsters[0];
        boss.Enraged = true;

        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 5),
            surprise: SurpriseState.None);
        var log = RunOneRound(engine, party);

        Assert.Equal(2, BossSwingLines(log, "Ogre"));
    }

    [Fact]
    public void A_normal_boss_strikes_once_in_a_round()
    {
        var party = MakeDefenders(2);
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Ogre, 1) }, isBoss: true);

        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 5),
            surprise: SurpriseState.None);
        var log = RunOneRound(engine, party);

        Assert.Equal(1, BossSwingLines(log, "Ogre"));
    }

    [Fact]
    public void A_wandering_monster_never_enrages()
    {
        var party = MakeDefenders(2);
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Ogre, 1) }); // not a boss
        var monster = encounter.Groups[0].Monsters[0];
        monster.HitPoints = 1; // would be "cornered" if it could enrage

        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 3),
            surprise: SurpriseState.None);
        var log = RunOneRound(engine, party);

        Assert.False(monster.Enraged);
        Assert.DoesNotContain(log, l => l.Contains("fury"));
    }

    [Fact]
    public void An_elite_champion_can_enrage()
    {
        var party = MakeDefenders(2);
        var elite = Elites.Promote(Bestiary.Ogre);
        var encounter = new Encounter(new[] { new MonsterGroup(elite, 1) });
        var champ = encounter.Groups[0].Monsters[0];
        champ.HitPoints = 5; // below 35% of the elite's inflated max

        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 3),
            surprise: SurpriseState.None);
        RunOneRound(engine, party);

        Assert.True(champ.Enraged, "an elite champion is a mini-boss and should enrage too");
    }

    [Fact]
    public void Monsters_focus_the_weakest_hero_more_often_than_chance()
    {
        const int trials = 400;
        var weakHits = 0;
        var totalHits = 0;

        for (var i = 0; i < trials; i++)
        {
            var factory = new CharacterFactory(new SystemRandomSource(seed: 11));
            var party = new Party();
            var strongA = factory.Create("StrongA", Race.Dwarf, CharacterClass.Warrior);
            var strongB = factory.Create("StrongB", Race.Dwarf, CharacterClass.Warrior);
            var weak = factory.Create("Weak", Race.Human, CharacterClass.Rogue);
            foreach (var (c, hp) in new[] { (strongA, 60), (strongB, 60), (weak, 12) })
            {
                c.MaxHitPoints = hp;
                c.HitPoints = hp;
            }
            party.Add(strongA);
            party.Add(strongB);
            party.Add(weak);

            var before = party.Members.Select(m => m.HitPoints).ToList();
            var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Ogre, 1) });
            var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: i + 1),
                surprise: SurpriseState.None);

            engine.ExecuteRound(party.Members
                .Select(m => new CombatCommand(m, CombatActionType.Defend))
                .ToList());

            for (var m = 0; m < party.Members.Count; m++)
            {
                if (party.Members[m].HitPoints < before[m])
                {
                    totalHits++;
                    if (ReferenceEquals(party.Members[m], weak)) weakHits++;
                }
            }
        }

        Assert.True(totalHits > 50, $"expected a healthy sample of landed hits, got {totalHits}");
        var weakShare = (double)weakHits / totalHits;
        // Uniform targeting over three front-rank heroes would be ~0.33; the focus-fire
        // bias should push the weakest hero's share well above that.
        Assert.True(weakShare > 0.5, $"weakest-hero share was only {weakShare:P0}");
    }
}
