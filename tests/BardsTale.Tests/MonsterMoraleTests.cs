using System.Collections.Generic;
using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Covers monster morale: a cornered, outnumbered rank-and-file monster may break and rout
/// (leaving the field with no XP), while bosses, elites and the toughest brutes hold their ground.
/// </summary>
public class MonsterMoraleTests
{
    private static Party Defenders(int count = 4, int hp = 200)
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

    private static List<string> RunRounds(CombatEngine engine, Party party, int rounds, out bool fled)
    {
        var all = new List<string>();
        fled = false;
        for (var i = 0; i < rounds && !engine.IsOver; i++)
        {
            var log = engine.ExecuteRound(party.Members
                .Select(m => new CombatCommand(m, CombatActionType.Defend)).ToList()).Log;
            all.AddRange(log);
            if (log.Any(l => l.Contains("flees the battle"))) { fled = true; break; }
        }
        return all;
    }

    [Fact]
    public void A_cornered_outnumbered_monster_breaks_and_routs()
    {
        var party = Defenders();
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) });
        encounter.Groups[0].Monsters[0].HitPoints = 1; // desperate, and outnumbered 4-to-1

        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 3),
            surprise: SurpriseState.None);
        RunRounds(engine, party, 10, out var fled);

        Assert.True(fled, "a 1-HP lone rat against four foes should eventually rout");
        Assert.Equal(0, encounter.Groups[0].LivingCount);     // it left the field
        Assert.Equal(0, encounter.TotalExperience);           // no XP for a foe that escaped
    }

    [Fact]
    public void A_boss_never_routs()
    {
        var party = Defenders();
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Ogre, 1) }, isBoss: true);
        encounter.Groups[0].Monsters[0].HitPoints = 1; // cornered, but it is a boss

        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 3),
            surprise: SurpriseState.None);
        var log = RunRounds(engine, party, 10, out var fled);

        Assert.False(fled);
        Assert.DoesNotContain(log, l => l.Contains("flees the battle"));
    }

    [Fact]
    public void A_tough_brute_holds_its_ground()
    {
        var party = Defenders();
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Troll, 1) }); // 45 HP — fearless
        encounter.Groups[0].Monsters[0].HitPoints = 1;

        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 3),
            surprise: SurpriseState.None);
        var log = RunRounds(engine, party, 10, out var fled);

        Assert.False(fled);
        Assert.DoesNotContain(log, l => l.Contains("flees the battle"));
    }

    [Fact]
    public void A_healthy_monster_stands_and_fights()
    {
        var party = Defenders();
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) }); // full HP

        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 3),
            surprise: SurpriseState.None);
        var log = RunRounds(engine, party, 6, out var fled);

        Assert.False(fled);
        Assert.DoesNotContain(log, l => l.Contains("flees the battle"));
    }
}
