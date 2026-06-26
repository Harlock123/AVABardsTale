using System.Collections.Generic;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

/// <summary>Covers the combat-log classifier that colours/icons each line by event type.</summary>
public class CombatLogStyleTests
{
    private static readonly HashSet<string> Party = new() { "Brynn", "Sable" };

    private static string Icon(string line) => new CombatLogLineViewModel(line, Party).Icon;

    [Fact]
    public void A_party_member_hitting_a_foe_reads_as_an_attack()
        => Assert.Equal("⚔", Icon("Brynn hits Goblin for 8."));

    [Fact]
    public void A_foe_hitting_a_party_member_reads_as_a_blow_taken()
        => Assert.Equal("✸", Icon("Goblin hits Brynn for 5."));

    [Fact]
    public void Taking_damage_reads_as_a_blow_taken()
        => Assert.Equal("✸", Icon("Sable takes 5 damage."));

    [Fact]
    public void An_enemy_kill_and_an_ally_death_read_differently()
    {
        Assert.Equal("☠", Icon("Goblin is slain!"));        // enemy down
        Assert.Equal("⚰", Icon("Brynn has fallen!"));        // ally down
    }

    [Fact]
    public void Healing_and_regen_read_as_heals()
    {
        Assert.Equal("✚", Icon("Brynn regenerates 2 HP."));
        Assert.Equal("✚", Icon("Sable casts Healing Spring, healing Brynn for 9."));
    }

    [Fact]
    public void A_warded_blow_reads_as_a_ward_not_a_blow()
        => Assert.Equal("🛡", Icon("Brynn takes 6 damage — warded against fire."));

    [Fact]
    public void A_save_reads_as_a_ward()
        => Assert.Equal("🛡", Icon("Sable resists the slumber."));

    [Fact]
    public void A_miss_reads_faint()
        => Assert.Equal("·", Icon("Goblin misses the party."));

    [Fact]
    public void Victory_gets_the_banner_icon()
        => Assert.Equal("🏆", Icon("The enemies are defeated! (120 XP, 30 gold)"));

    [Fact]
    public void A_cast_reads_as_a_spell()
        => Assert.Equal("✨", Icon("Coven Witch casts Slumber!"));

    [Fact]
    public void Plain_narration_carries_no_icon()
        => Assert.False(new CombatLogLineViewModel("You face a band of monsters.", Party).HasIcon);
}
