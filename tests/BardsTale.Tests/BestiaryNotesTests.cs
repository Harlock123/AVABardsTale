using BardsTale.Core.Combat;
using BardsTale.Core.Lore;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

/// <summary>The bestiary's per-monster notes — including the telegraph hint for signature-casting bosses.</summary>
public class BestiaryNotesTests
{
    private static BestiaryEntryViewModel Discovered(MonsterTemplate t) =>
        new(t, new CodexEntry { Name = t.Name });

    [Fact]
    public void A_signature_boss_note_explains_its_telegraph()
    {
        // The Demon Lord casts Hellstorm (a BlastParty signature) — a telegraph-able boss.
        var note = Discovered(Bosses.DemonLord).Notes;
        Assert.Contains("winds up", note);
        Assert.Contains("brace or break", note);
    }

    [Fact]
    public void An_ordinary_caster_gets_no_telegraph_note()
    {
        // A rank-and-file caster casts, but never telegraphs.
        var note = Discovered(Bestiary.SparkImp).Notes;
        Assert.DoesNotContain("winds up", note);
    }
}
