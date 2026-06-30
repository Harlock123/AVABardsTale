using System.Collections.Generic;
using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Telegraphed, multi-phase bosses: worn below half health a lair boss enters a second phase and
/// can wind up a signature attack one round ahead — which the party can brace against (Defend) or
/// break by putting the boss to sleep before it strikes.
/// </summary>
public class TelegraphBossTests
{
    private static Party Defenders(int count = 3, int hp = 500)
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

    private static IReadOnlyList<string> Round(CombatEngine engine, Party party)
        => engine.ExecuteRound(party.Members.Where(m => !m.IsDead)
            .Select(m => new CombatCommand(m, CombatActionType.Defend)).ToList()).Log;

    // Demon Lord's signature is Hellstorm (a BlastParty spell) — a telegraph-able damage attack.
    private static (CombatEngine engine, Monster boss, Party party) Setup(int seed = 1, bool isBoss = true)
    {
        var party = Defenders();
        var encounter = new Encounter(new[] { new MonsterGroup(Bosses.DemonLord, 1) }, isBoss: isBoss);
        var boss = encounter.Groups[0].Monsters[0];
        boss.HitPoints = boss.Template.MaxHitPoints / 2; // at the phase-two threshold
        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed), surprise: SurpriseState.None);
        return (engine, boss, party);
    }

    [Fact]
    public void A_worn_boss_shifts_into_its_second_phase()
    {
        var (engine, boss, party) = Setup();
        var log = Round(engine, party);
        Assert.Equal(2, boss.Phase);
        Assert.Contains(log, l => l.Contains("deadlier"));
    }

    [Fact]
    public void A_phase_two_boss_eventually_telegraphs_its_signature()
    {
        var (engine, boss, party) = Setup();
        var sawWindUp = false;
        for (var i = 0; i < 20 && !sawWindUp; i++)
            sawWindUp = Round(engine, party).Any(l => l.Contains("gathers power"));
        Assert.True(sawWindUp, "a phase-two boss should wind up a signature attack");
    }

    [Fact]
    public void A_charged_signature_unleashes_on_the_next_turn_and_bracing_softens_it()
    {
        var (engine, boss, party) = Setup();
        boss.Phase = 2;
        boss.Charging = BossSignatures.For(boss.Template); // queue the signature directly for determinism

        var hpBefore = party.Members[0].HitPoints;
        var log = Round(engine, party); // everyone Defends — they brace

        Assert.Contains(log, l => l.Contains("unleashes"));
        Assert.Contains(log, l => l.Contains("braced"));
        Assert.Null(boss.Charging);
        Assert.True(party.Members[0].HitPoints < hpBefore, "the blast should still bite through the brace");
    }

    [Fact]
    public void Sleeping_a_charging_boss_breaks_the_telegraph()
    {
        var (engine, boss, party) = Setup();
        boss.Phase = 2;
        boss.Charging = BossSignatures.For(boss.Template);
        boss.Sleep(2); // stunned before it can release

        var log = Round(engine, party);

        Assert.DoesNotContain(log, l => l.Contains("unleashes"));
        Assert.Contains(log, l => l.Contains("unravels"));
        Assert.Null(boss.Charging);
    }

    [Fact]
    public void Marquee_bosses_have_bespoke_named_signatures()
    {
        Assert.Equal("Hellfire Cataclysm", BossSignatures.For(Bosses.DemonLord)!.Name);
        Assert.Equal("Mind Storm", BossSignatures.For(Bosses.Mangar)!.Name);
        var winter = BossSignatures.For(Bosses.FrostKing)!;
        Assert.Equal("Killing Winter", winter.Name);
        Assert.Equal(StatusEffect.Paralyzed, winter.Rider); // it can freeze a hero solid
        // A boss with no damaging spell (the sleep-casting Coven Matron) has no signature to telegraph.
        Assert.Null(BossSignatures.For(Bosses.CovenMatron));
    }

    [Fact]
    public void The_frost_kings_signature_can_freeze_the_party()
    {
        var party = Defenders(hp: 2000); // tanky enough to weather the blast and show the freeze
        var encounter = new Encounter(new[] { new MonsterGroup(Bosses.FrostKing, 1) }, isBoss: true);
        var boss = encounter.Groups[0].Monsters[0];
        boss.Phase = 2;
        boss.Charging = BossSignatures.For(boss.Template);
        // Attack (not Defend) so no bracing; many heroes maximise the chance a rider lands.
        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 4), surprise: SurpriseState.None);

        var froze = false;
        for (var i = 0; i < 8 && !froze; i++)
        {
            boss.Phase = 2;
            boss.Charging = BossSignatures.For(boss.Template);
            var log = engine.ExecuteRound(party.Members.Where(m => m.CanAct)
                .Select(m => new CombatCommand(m, CombatActionType.Defend, 0)).ToList()).Log;
            froze = log.Any(l => l.Contains("frozen rigid"));
        }
        Assert.True(froze, "Killing Winter should eventually freeze a hero");
    }

    [Fact]
    public void Ordinary_encounters_never_telegraph()
    {
        // The same template outside a boss lair (isBoss: false) never shifts phase or telegraphs.
        var (engine, boss, party) = Setup(isBoss: false);
        for (var i = 0; i < 20; i++)
        {
            var log = Round(engine, party);
            Assert.DoesNotContain(log, l => l.Contains("gathers power"));
        }
        Assert.Equal(1, boss.Phase);
    }
}
