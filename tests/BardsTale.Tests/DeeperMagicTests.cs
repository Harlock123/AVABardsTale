using System.Collections.Generic;
using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Items;
using BardsTale.Core.Magic;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Covers the deeper-magic expansion: higher-tier spells (incl. the new Resurrection Field),
/// two new sustained Bard songs, and the boss-exclusive signature legendaries.
/// </summary>
public class DeeperMagicTests
{
    private static Party TankyParty()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        foreach (var m in party.Members) { m.MaxHitPoints = 9999; m.HitPoints = 9999; m.MaxSpellPoints = 999; m.SpellPoints = 999; }
        return party;
    }

    private static (CombatEngine engine, Party party, Character bard) SoloBard(out Encounter enc, string extraSong)
    {
        var factory = new CharacterFactory(new SystemRandomSource(seed: 5));
        var bard = factory.Create("Lyric", Race.HalfElf, CharacterClass.Bard);
        bard.MaxHitPoints = 80; bard.HitPoints = 80;
        bard.MaxSpellPoints = 60; bard.SpellPoints = 10;
        if (!bard.KnownSongs.Contains(extraSong)) bard.KnownSongs.Add(extraSong);
        bard.BardTunes = 5;
        var party = new Party();
        party.Add(bard);
        enc = new Encounter(new[] { new MonsterGroup(Bestiary.Ogre, 1) }); // tanky, survives a few rounds
        var engine = new CombatEngine(party, enc, new SystemRandomSource(seed: 2), surprise: SurpriseState.None);
        return (engine, party, bard);
    }

    // --- Higher-tier spells ---

    [Fact]
    public void Each_school_gains_level_four_and_five_spells()
    {
        foreach (var school in new[] { MagicSchool.Conjurer, MagicSchool.Magician, MagicSchool.Sorcerer, MagicSchool.Wizard })
        {
            Assert.NotEmpty(Spells.LearnedAtLevel(school, 4));
            Assert.NotEmpty(Spells.LearnedAtLevel(school, 5));
        }
    }

    [Fact]
    public void Resurrection_field_raises_every_fallen_ally_at_once()
    {
        var party = TankyParty();
        var caster = party.Members[0];
        caster.SpellPoints = 50;
        party.Members[1].ApplyDamage(99999);
        party.Members[2].ApplyDamage(99999);
        Assert.True(party.Members[1].IsDead && party.Members[2].IsDead);

        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) });
        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 3), surprise: SurpriseState.None);

        engine.ExecuteRound(new List<CombatCommand>
        {
            new(caster, CombatActionType.CastSpell, 0, Spell: Spells.Get("RESF"))
        });

        Assert.False(party.Members[1].IsDead);
        Assert.False(party.Members[2].IsDead);
    }

    [Fact]
    public void Annihilation_hits_far_harder_than_the_starter_bolt()
    {
        var spread = Spells.Get("ANNI").Power - Spells.Get("MABL").Power;
        Assert.True(spread > 25, "the capstone Wizard nuke should dwarf Mage's Bolt");
    }

    // --- New sustained songs ---

    [Fact]
    public void The_cantata_of_mana_restores_party_spell_points_each_round()
    {
        var (engine, _, bard) = SoloBard(out _, "MANA");
        bard.SpellPoints = 10;

        var round = engine.ExecuteRound(new[] { new CombatCommand(bard, CombatActionType.Sing, Song: Songs.Get("MANA")) });

        Assert.Contains(round.Log, l => l.Contains("spell points"));
        Assert.True(bard.SpellPoints > 10, "the cantata should feed SP back");
    }

    [Fact]
    public void The_dirge_of_the_doomed_withers_every_foe_each_round()
    {
        var (engine, _, bard) = SoloBard(out var enc, "DIRG");
        var hpBefore = enc.Groups[0].Monsters[0].HitPoints;

        var round = engine.ExecuteRound(new[] { new CombatCommand(bard, CombatActionType.Sing, Song: Songs.Get("DIRG")) });

        Assert.Contains(round.Log, l => l.Contains("dirge"));
        Assert.True(enc.Groups[0].Monsters[0].HitPoints < hpBefore, "the dirge should damage the foe");
    }

    // --- Signature legendaries ---

    [Fact]
    public void Each_legendary_is_a_strong_weapon_with_an_active_power()
    {
        Assert.NotEmpty(Items.Legendaries);
        foreach (var leg in Items.Legendaries)
        {
            Assert.Equal(ItemSlot.Weapon, leg.Slot);
            Assert.True(leg.MagicBonus >= 3, $"{leg.Name} should be a potent weapon");
            Assert.True(leg.HasPower, $"{leg.Name} should carry a once-per-fight power");
        }
    }

    [Fact]
    public void Named_bosses_guarantee_their_legendary_and_it_round_trips()
    {
        // Each mapped boss drops its legendary; it isn't in the common power-item pool.
        foreach (var boss in new[] { "Demon Lord", "Lich King", "Death Tyrant", "Dragon Tyrant" })
        {
            var leg = Items.LegendaryForBoss(boss);
            Assert.NotNull(leg);
            Assert.DoesNotContain(leg!, Items.PowerItems);
            Assert.True(Items.ByName.ContainsKey(leg.Name), "legendaries must resolve on save load");
        }
        Assert.Null(Items.LegendaryForBoss("Goblin King")); // not every boss has one
    }

    [Fact]
    public void A_legendary_power_devastates_the_enemy()
    {
        var party = TankyParty();
        var wielder = party.Members[0];
        wielder.Weapon = Items.Wyrmslayer;

        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Skeleton, 3) });
        var before = encounter.Groups[0].Monsters.Sum(m => m.HitPoints);
        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 4), surprise: SurpriseState.None);

        engine.ExecuteRound(new List<CombatCommand>
        {
            new(wielder, CombatActionType.CastSpell, 0, Spell: Items.Wyrmslayer.ItemPower)
        });

        var after = encounter.Groups[0].Monsters.Sum(m => m.HitPoints);
        Assert.True(after < before, "Dragonfire should scorch the whole group");
    }
}
