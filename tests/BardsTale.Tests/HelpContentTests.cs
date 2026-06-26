using System.Linq;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

/// <summary>Covers the help/tutorial overlay content — coverage of the core systems and the live key bindings.</summary>
public class HelpContentTests
{
    [Fact]
    public void Help_covers_the_core_systems()
    {
        var sections = HelpContent.Build("W", "S", "A", "D");
        var titles = sections.Select(s => s.Title.ToLowerInvariant()).ToList();

        Assert.Contains(titles, t => t.Contains("start"));     // getting started
        Assert.Contains(titles, t => t.Contains("moving"));    // controls
        Assert.Contains(titles, t => t.Contains("town"));      // town services
        Assert.Contains(titles, t => t.Contains("catacomb"));  // dungeon
        Assert.Contains(titles, t => t.Contains("combat"));    // combat
    }

    [Fact]
    public void Every_section_has_at_least_one_line()
    {
        foreach (var section in HelpContent.Build("W", "S", "A", "D"))
        {
            Assert.False(string.IsNullOrWhiteSpace(section.Title));
            Assert.NotEmpty(section.Lines);
        }
    }

    [Fact]
    public void The_controls_section_shows_the_current_movement_bindings()
    {
        var sections = HelpContent.Build("FWD", "BCK", "LFT", "RGT");
        var moving = sections.First(s => s.Title.ToLowerInvariant().Contains("moving"));
        var text = string.Join(" ", moving.Lines);

        Assert.Contains("FWD", text);
        Assert.Contains("BCK", text);
        Assert.Contains("LFT", text);
        Assert.Contains("RGT", text);
    }
}
