using System.Collections.Generic;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Geometry;
using BardsTale.Core.Persistence;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Dungeon shrines that grant a dive-long party boon (a "player-side affix") for a gold offering,
/// and the boons' effect in combat.
/// </summary>
public class ShrineBoonTests
{
    private static GameState ShrineGame(out Party party, int gold)
    {
        var rng = new SystemRandomSource(seed: 1);
        party = NewGame.CreateDefaultParty(rng);
        party.Gold = gold;
        var maze = new Maze("t", 5, 5) { StartPosition = new Position(2, 2), StartFacing = Direction.North };
        maze[2, 2].Feature = CellFeature.Shrine;
        return new GameState(party, maze, rng);
    }

    [Fact]
    public void A_shrine_offering_grants_a_boon_and_takes_the_gold()
    {
        var game = ShrineGame(out var party, gold: 10_000);
        var before = party.Gold;

        var result = game.MakeOffering();

        Assert.True(result.Granted);
        Assert.NotEqual(PartyBoon.None, party.Boon);
        Assert.True(party.Gold < before);
        Assert.False(game.OnShrine); // the shrine is spent
    }

    [Fact]
    public void A_pauper_party_cannot_buy_a_boon()
    {
        var game = ShrineGame(out var party, gold: 0);

        var result = game.MakeOffering();

        Assert.False(result.Granted);
        Assert.Equal(PartyBoon.None, party.Boon);
        Assert.True(game.OnShrine); // still standing, nothing spent
    }

    [Fact]
    public void The_vigor_boon_mends_the_party_each_round()
    {
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 1));
        var hero = party.Members[0];
        hero.MaxHitPoints = 100;
        hero.HitPoints = 50;
        party.Boon = PartyBoon.Vigor;
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) });
        // MonstersSurprised → the rat skips the round, so only the Vigor aura moves the hero's HP.
        var engine = new CombatEngine(party, encounter, new SystemRandomSource(seed: 7), surprise: SurpriseState.MonstersSurprised);

        engine.ExecuteRound(new List<CombatCommand> { new(hero, CombatActionType.Defend, 0) });

        Assert.True(hero.HitPoints > 50, "Vigor should mend the party each round of battle");
    }

    [Fact]
    public void A_boon_survives_save_and_load()
    {
        var session = new GameSession(seed: 7);
        session.FillDefaultParty();
        session.Party.Boon = PartyBoon.Warding;

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session));

        Assert.Equal(PartyBoon.Warding, loaded.Party.Boon);
    }
}
