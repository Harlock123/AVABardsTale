using System.Collections.Generic;
using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Magic;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Covers Bard songs as sustained effects: a song lasts only while the Bard keeps playing,
/// starting a new tune spends one of the Bard's limited daily tunes, and resting refills them.
/// </summary>
public class SustainedSongTests
{
    private static (CombatEngine engine, Character bard) SoloBard(out Party party, int tunes = 4)
    {
        var factory = new CharacterFactory(new SystemRandomSource(seed: 5));
        var bard = factory.Create("Lyric", Race.HalfElf, CharacterClass.Bard);
        bard.MaxHitPoints = 60;
        bard.HitPoints = 60;
        if (!bard.KnownSongs.Contains("RENE")) bard.KnownSongs.Add("RENE"); // grant the regen hymn for testing
        bard.BardTunes = tunes;

        party = new Party();
        party.Add(bard);
        // A single harmless rat keeps combat going without threatening the bard or routing.
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) });
        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 2), surprise: SurpriseState.None);
        return (engine, bard);
    }

    private static CombatRound Sing(CombatEngine engine, Character bard, string songId)
        => engine.ExecuteRound(new[] { new CombatCommand(bard, CombatActionType.Sing, Song: Songs.Get(songId)) });

    [Fact]
    public void Starting_a_song_spends_a_tune_but_sustaining_it_is_free()
    {
        var (engine, bard) = SoloBard(out _, tunes: 4);

        Sing(engine, bard, "FALK");
        Assert.Equal(3, bard.BardTunes);     // a fresh tune is struck up

        Sing(engine, bard, "FALK");
        Assert.Equal(3, bard.BardTunes);     // sustaining the same song is free

        Sing(engine, bard, "SANC");
        Assert.Equal(2, bard.BardTunes);     // switching costs another tune
    }

    [Fact]
    public void A_bard_with_no_tunes_cannot_strike_up_a_song()
    {
        var (engine, bard) = SoloBard(out _, tunes: 0);

        var round = Sing(engine, bard, "FALK");

        Assert.Equal(0, bard.BardTunes);
        Assert.Contains(round.Log, l => l.Contains("voice is spent"));
    }

    [Fact]
    public void A_sustained_regen_song_works_while_sung_and_lapses_when_the_bard_stops()
    {
        var (engine, bard) = SoloBard(out _, tunes: 4);

        var singing = Sing(engine, bard, "RENE");
        Assert.Contains(singing.Log, l => l.Contains("mends the party for 3")); // the hymn's per-round regen

        var stopped = engine.ExecuteRound(new[] { new CombatCommand(bard, CombatActionType.Defend) });
        Assert.Contains(stopped.Log, l => l.Contains("Hymn of Renewal") && l.Contains("fade")); // it lapses
        Assert.DoesNotContain(stopped.Log, l => l.Contains("mends the party for 3"));            // no more regen
    }

    [Fact]
    public void Resting_refreshes_a_bards_tunes()
    {
        var factory = new CharacterFactory(new SystemRandomSource(seed: 5));
        var bard = factory.Create("Lyric", Race.HalfElf, CharacterClass.Bard);
        bard.BardTunes = 0;

        var party = new Party();
        party.Add(bard);
        party.Rest();

        Assert.Equal(bard.MaxBardTunes, bard.BardTunes);
        Assert.True(bard.BardTunes > 0);
    }

    [Fact]
    public void Bard_tunes_survive_save_load()
    {
        var session = new BardsTale.Core.Game.GameSession(seed: 9);
        session.FillDefaultParty();
        var bard = session.Party.Members.First(m => m.IsBard);
        bard.BardTunes = 2; // spent some on the way down

        var loaded = BardsTale.Core.Persistence.GameSerializer
            .FromJson(BardsTale.Core.Persistence.GameSerializer.ToJson(session));
        var loadedBard = loaded.Party.Members.First(m => m.IsBard);

        Assert.Equal(2, loadedBard.BardTunes);
    }
}
