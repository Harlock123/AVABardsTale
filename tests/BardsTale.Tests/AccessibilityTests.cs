using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using Avalonia.Media;
using BardsTale.UI;
using BardsTale.UI.Settings;
using BardsTale.UI.ViewModels;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Covers the accessibility pass: the colourblind-friendly combat-log palette and the rebindable
/// movement keys (with the arrow keys always honoured).
/// </summary>
public class AccessibilityTests
{
    private static Color Of(LogCategory c, bool colorblind) =>
        ((SolidColorBrush)CombatLogPalette.BrushFor(c, colorblind)).Color;

    [Fact]
    public void The_colorblind_palette_keeps_the_confusable_categories_distinct()
    {
        // The categories most prone to red/green confusion must all read as different colours.
        var confusable = new[]
        {
            LogCategory.PartyHit, LogCategory.EnemyHit, LogCategory.Heal,
            LogCategory.EnemyDown, LogCategory.Grave, LogCategory.Ward
        };
        var colors = confusable.Select(c => Of(c, colorblind: true)).ToList();
        Assert.Equal(colors.Count, colors.Distinct().Count());

        // And it actually changes the harm/heal colours from the default scheme.
        Assert.NotEqual(Of(LogCategory.EnemyHit, false), Of(LogCategory.EnemyHit, true));
        Assert.NotEqual(Of(LogCategory.Heal, false), Of(LogCategory.Heal, true));
    }

    [Fact]
    public void A_log_line_takes_its_colour_from_the_active_palette()
    {
        var party = new HashSet<string> { "Brynn" };
        var saved = AppSettings.Current.ColorblindMode;
        try
        {
            AppSettings.Current.ColorblindMode = true;
            var hurt = new CombatLogLineViewModel("Goblin hits Brynn for 5.", party); // EnemyHit
            Assert.Equal(LogCategory.EnemyHit, hurt.Category);
            Assert.Same(CombatLogPalette.BrushFor(LogCategory.EnemyHit, true), hurt.Brush);

            AppSettings.Current.ColorblindMode = false;
            var hurt2 = new CombatLogLineViewModel("Goblin hits Brynn for 5.", party);
            Assert.Same(CombatLogPalette.BrushFor(LogCategory.EnemyHit, false), hurt2.Brush);
        }
        finally { AppSettings.Current.ColorblindMode = saved; }
    }

    [Fact]
    public void Movement_resolves_the_default_keys_and_the_arrows()
    {
        Assert.Equal(MovementKeys.MoveAction.Forward, MovementKeys.Resolve(Key.W));
        Assert.Equal(MovementKeys.MoveAction.Backward, MovementKeys.Resolve(Key.S));
        Assert.Equal(MovementKeys.MoveAction.Left, MovementKeys.Resolve(Key.A));
        Assert.Equal(MovementKeys.MoveAction.Right, MovementKeys.Resolve(Key.D));

        Assert.Equal(MovementKeys.MoveAction.Forward, MovementKeys.Resolve(Key.Up));
        Assert.Equal(MovementKeys.MoveAction.Backward, MovementKeys.Resolve(Key.Down));
        Assert.Equal(MovementKeys.MoveAction.Left, MovementKeys.Resolve(Key.Left));
        Assert.Equal(MovementKeys.MoveAction.Right, MovementKeys.Resolve(Key.Right));

        Assert.Equal(MovementKeys.MoveAction.None, MovementKeys.Resolve(Key.Z));
    }

    [Fact]
    public void Rebinding_a_movement_key_takes_effect_while_arrows_still_work()
    {
        var s = AppSettings.Current;
        var savedForward = s.MoveForwardKey;
        try
        {
            s.MoveForwardKey = Key.I;
            Assert.Equal(MovementKeys.MoveAction.Forward, MovementKeys.Resolve(Key.I));
            Assert.Equal(MovementKeys.MoveAction.Forward, MovementKeys.Resolve(Key.Up)); // arrows are a fixed fallback
            Assert.Equal(MovementKeys.MoveAction.None, MovementKeys.Resolve(Key.W));     // the old key no longer moves
        }
        finally { s.MoveForwardKey = savedForward; }
    }
}
