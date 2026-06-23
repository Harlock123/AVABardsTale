using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Geometry;
using BardsTale.Core.Magic;
using BardsTale.Core.Util;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

public class AntiMagicTests
{
    [Fact]
    public void Stepping_into_an_anti_magic_zone_suppresses_magic()
    {
        var rng = new SystemRandomSource(seed: 1);
        var party = NewGame.CreateDefaultParty(rng);
        var maze = new Maze("t", 5, 5) { StartPosition = new Position(2, 2), StartFacing = Direction.North };
        maze[2, 1].Feature = CellFeature.AntiMagic;
        var game = new GameState(party, maze, rng);

        var result = game.StepForward();

        Assert.Equal(MoveResultKind.AntiMagic, result.Kind);
        Assert.True(game.MagicSuppressed);
    }

    [Fact]
    public void Generated_levels_include_anti_magic_zones()
    {
        var maze = new MazeBuilder(new SystemRandomSource(seed: 42)).Build("t", 16, 16);
        var found = false;
        for (var x = 0; x < maze.Width && !found; x++)
            for (var y = 0; y < maze.Height; y++)
                if (maze[x, y].Feature == CellFeature.AntiMagic) { found = true; break; }
        Assert.True(found);
    }

    [Fact]
    public void Spells_fizzle_in_an_anti_magic_fight()
    {
        var rng = new SystemRandomSource(seed: 8);
        var party = NewGame.CreateDefaultParty(new SystemRandomSource(seed: 8));
        var mage = party.Members.First(m => m.Class == CharacterClass.Magician);

        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Skeleton, 1) });
        var skeleton = encounter.Groups[0].Monsters[0];
        var hpBefore = skeleton.HitPoints;

        var engine = new CombatEngine(party, encounter, rng, magicSuppressed: true, surprise: SurpriseState.None);
        var round = engine.ExecuteRound(new[]
        {
            new CombatCommand(mage, CombatActionType.CastSpell, 0, Spell: Spells.Get("ARFI"))
        });

        Assert.Equal(hpBefore, skeleton.HitPoints); // the fire bolt never lands
        Assert.Contains(round.Log, l => l.Contains("sputters"));
    }

    [Fact]
    public void Combat_offers_no_spells_in_an_anti_magic_zone()
    {
        var party = OneMageParty();
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) });

        var suppressed = new CombatViewModel(party, encounter, new SystemRandomSource(seed: 1),
            magicSuppressed: true, surprise: SurpriseState.None);
        suppressed.Begin();
        Assert.DoesNotContain(suppressed.Options, o => o.Action == CombatActionType.CastSpell);

        var normal = new CombatViewModel(OneMageParty(),
            new Encounter(new[] { new MonsterGroup(Bestiary.GiantRat, 1) }), new SystemRandomSource(seed: 1),
            surprise: SurpriseState.None);
        normal.Begin();
        Assert.Contains(normal.Options, o => o.Action == CombatActionType.CastSpell);
    }

    [Fact]
    public void Light_cannot_be_conjured_in_an_anti_magic_zone()
    {
        var rng = new SystemRandomSource(seed: 1);
        var party = NewGame.CreateDefaultParty(rng);
        var maze = new Maze("t", 5, 5) { StartPosition = new Position(2, 2), StartFacing = Direction.North };
        maze[2, 2].Feature = CellFeature.AntiMagic; // the party starts standing in it
        var vm = new ExplorationViewModel(new GameState(party, maze, rng));

        vm.CastLightCommand.Execute(null);

        Assert.False(vm.HasLight);
    }

    private static Party OneMageParty()
    {
        var party = new Party();
        party.Add(new CharacterFactory(new SystemRandomSource(seed: 1))
            .Create("Vex", Race.Gnome, CharacterClass.Magician));
        return party;
    }
}
