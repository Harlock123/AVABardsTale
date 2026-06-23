using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

public class MonsterCasterTests
{
    private static MonsterTemplate Healer => new(
        "Test Healer", 20, 6, 1, 4, 0, 0, 0, 1, Speed: 1,
        Spell: new MonsterSpell("Mend", MonsterSpellKind.HealAllies, Power: 10, Chance: 1.0));

    private static MonsterTemplate Sleeper => new(
        "Test Sleeper", 12, 7, 1, 4, 0, 0, 0, 1, Speed: 1,
        Spell: new MonsterSpell("Slumber", MonsterSpellKind.SleepFoe, Power: 6, Chance: 1.0));

    private static MonsterTemplate Blaster => new(
        "Test Blaster", 12, 7, 1, 4, 0, 0, 0, 1, Speed: 1,
        Spell: new MonsterSpell("Firestorm", MonsterSpellKind.BlastParty, Power: 6, Chance: 1.0));

    private static MonsterTemplate Bolter => new(
        "Test Bolter", 12, 7, 1, 4, 0, 0, 0, 1, Speed: 1,
        Spell: new MonsterSpell("Bolt", MonsterSpellKind.DamageFoe, Power: 14, Chance: 1.0));

    [Fact]
    public void Bestiary_includes_a_sleep_caster_and_a_healer()
    {
        Assert.Equal(MonsterSpellKind.SleepFoe, Bestiary.CovenWitch.Spell!.Kind);
        Assert.Equal(MonsterSpellKind.HealAllies, Bestiary.MadGodAcolyte.Spell!.Kind);
    }

    [Fact]
    public void A_healer_monster_mends_a_wounded_ally()
    {
        var rng = new SystemRandomSource(seed: 3);
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 3));

        var woundedGroup = new MonsterGroup(Bestiary.Berserker, 1);
        var wounded = woundedGroup.Monsters[0];
        wounded.HitPoints = 4;

        var encounter = new Encounter(new[] { woundedGroup, new MonsterGroup(Healer, 1) });
        var engine = new CombatEngine(party, encounter, rng, surprise: SurpriseState.None);

        var commands = party.Members.Where(m => m.CanAct)
            .Select(m => new CombatCommand(m, CombatActionType.Defend)).ToList();
        var round = engine.ExecuteRound(commands);

        Assert.True(wounded.HitPoints > 4, "the healer should have mended the wounded ally");
        Assert.Contains(round.Log, l => l.Contains("Mend"));
    }

    [Fact]
    public void A_sleep_caster_can_put_party_members_to_sleep()
    {
        var rng = new SystemRandomSource(seed: 3);
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 3));

        var encounter = new Encounter(new[] { new MonsterGroup(Sleeper, 1) });
        var engine = new CombatEngine(party, encounter, rng, surprise: SurpriseState.None);

        var commands = party.Members.Where(m => m.CanAct)
            .Select(m => new CombatCommand(m, CombatActionType.Defend)).ToList();
        var round = engine.ExecuteRound(commands);

        Assert.Contains(round.Log, l => l.Contains("Slumber"));
        Assert.Contains(party.Members, m => m.IsAsleep);
    }

    [Fact]
    public void A_blast_spell_damages_the_whole_party()
    {
        var rng = new SystemRandomSource(seed: 3);
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 3));
        var before = party.Members.Sum(m => m.HitPoints);
        var livingBefore = party.LivingCount;

        var encounter = new Encounter(new[] { new MonsterGroup(Blaster, 1) });
        var engine = new CombatEngine(party, encounter, rng, surprise: SurpriseState.None);

        var commands = party.Members.Where(m => m.CanAct)
            .Select(m => new CombatCommand(m, CombatActionType.Defend)).ToList();
        var round = engine.ExecuteRound(commands);

        Assert.Contains(round.Log, l => l.Contains("Firestorm"));
        // Every living member should have taken at least 1 point of blast damage.
        Assert.True(party.Members.Sum(m => m.HitPoints) <= before - livingBefore);
    }

    [Fact]
    public void A_bolt_spell_strikes_a_single_member_hard()
    {
        var rng = new SystemRandomSource(seed: 3);
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 3));
        var before = party.Members.Sum(m => m.HitPoints);

        var encounter = new Encounter(new[] { new MonsterGroup(Bolter, 1) });
        var engine = new CombatEngine(party, encounter, rng, surprise: SurpriseState.None);

        var commands = party.Members.Where(m => m.CanAct)
            .Select(m => new CombatCommand(m, CombatActionType.Defend)).ToList();
        var round = engine.ExecuteRound(commands);

        Assert.Contains(round.Log, l => l.Contains("Bolt"));
        Assert.True(party.Members.Sum(m => m.HitPoints) < before);
    }

    [Fact]
    public void A_healer_with_no_wounded_allies_attacks_instead()
    {
        var rng = new SystemRandomSource(seed: 3);
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 3));

        // Healer alone and at full health: nothing to heal, so it must attack.
        var encounter = new Encounter(new[] { new MonsterGroup(Healer, 1) });
        var engine = new CombatEngine(party, encounter, rng, surprise: SurpriseState.None);

        var commands = party.Members.Where(m => m.CanAct)
            .Select(m => new CombatCommand(m, CombatActionType.Defend)).ToList();
        var round = engine.ExecuteRound(commands);

        Assert.DoesNotContain(round.Log, l => l.Contains("Mend"));
    }
}
