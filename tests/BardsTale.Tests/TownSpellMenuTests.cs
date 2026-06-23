using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Game;
using BardsTale.Core.Magic;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

public class TownSpellMenuTests
{
    private static TownViewModel Town(out GameSession session)
    {
        session = new GameSession(seed: 2);
        session.FillDefaultParty();
        return new TownViewModel(session);
    }

    [Fact]
    public void Only_restorative_spells_are_usable_in_town()
    {
        Assert.True(Spells.Get("VOPL").UsableInTown);  // heal ally
        Assert.True(Spells.Get("HEPA").UsableInTown);  // heal party
        Assert.True(Spells.Get("PURE").UsableInTown);  // cure
        Assert.True(Spells.Get("REST").UsableInTown);  // revive
        Assert.False(Spells.Get("ARFI").UsableInTown); // damage
        Assert.False(Spells.Get("AROF").UsableInTown); // buff
        Assert.False(Spells.Get("SCSI").UsableInTown); // identify
    }

    [Fact]
    public void Opening_the_menu_lists_castable_spells_and_blocks_movement()
    {
        var town = Town(out _);

        town.OpenSpellMenuCommand.Execute(null);

        Assert.True(town.IsSpellMenuOpen);
        Assert.NotEmpty(town.SpellMenu); // the Conjurer knows Vorpal Plating
        Assert.False(town.CanExplore);
        Assert.False(town.MoveForwardCommand.CanExecute(null));
    }

    [Fact]
    public void Casting_a_heal_restores_a_wounded_ally_for_spell_points()
    {
        var town = Town(out var session);
        var wounded = session.Party.Members[0];
        wounded.HitPoints = 1;

        town.OpenSpellMenuCommand.Execute(null);
        town.SelectedSpell = town.SpellMenu.First(s => s.Spell.Effect == SpellEffect.HealAlly);
        town.SelectedHero = town.Party.First(h => h.Model == wounded);
        var caster = town.SelectedSpell.Caster;
        var spBefore = caster.SpellPoints;

        town.CastTownSpellCommand.Execute(null);

        Assert.True(wounded.HitPoints > 1);
        Assert.True(caster.SpellPoints < spBefore);
    }

    [Fact]
    public void Casting_revive_in_town_raises_a_fallen_ally()
    {
        var town = Town(out var session);
        var caster = session.Party.Members.First(m => m.IsSpellcaster);
        caster.KnownSpells.Add("REST"); // grant a revive spell
        caster.SpellPoints = 20;
        var fallen = session.Party.Members[1];
        fallen.ApplyDamage(fallen.MaxHitPoints + 10);
        Assert.True(fallen.IsDead);

        town.OpenSpellMenuCommand.Execute(null);
        town.SelectedSpell = town.SpellMenu.First(s => s.Spell.Effect == SpellEffect.Revive);
        town.SelectedHero = town.Party.First(h => h.Model == fallen);
        town.CastTownSpellCommand.Execute(null);

        Assert.False(fallen.IsDead);
    }
}
