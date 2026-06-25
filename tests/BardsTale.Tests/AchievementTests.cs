using System.Linq;
using BardsTale.Core.Game;
using BardsTale.Core.Persistence;
using Xunit;

namespace BardsTale.Tests;

public class AchievementTests
{
    [Fact]
    public void Reaching_a_depth_unlocks_its_achievement_and_grants_renown()
    {
        var renown = new RenownLog();
        var newly = renown.Sync(new RunStats { DeepestDepth = 5 }, bestiaryDiscovered: 0, questsCompleted: 0);

        Assert.Contains(newly, a => a.Id == "DEPTH2");
        Assert.Contains(newly, a => a.Id == "DEPTH5");
        Assert.DoesNotContain(newly, a => a.Id == "DEPTH10");
        Assert.True(renown.IsUnlocked("DEPTH5"));
        Assert.Equal(15, renown.Renown); // 5 + 10
    }

    [Fact]
    public void Syncing_again_unlocks_nothing_new()
    {
        var renown = new RenownLog();
        renown.Sync(new RunStats { DeepestDepth = 5 }, 0, 0);
        Assert.Empty(renown.Sync(new RunStats { DeepestDepth = 5 }, 0, 0));
    }

    [Fact]
    public void The_victory_achievement_requires_mangar_beaten()
    {
        var renown = new RenownLog();
        Assert.DoesNotContain(renown.Sync(new RunStats { DeepestDepth = 20 }, 0, 0), a => a.Id == "MANGAR");
        Assert.Contains(renown.Sync(new RunStats { DeepestDepth = 20, Victory = true }, 0, 0), a => a.Id == "MANGAR");
    }

    [Fact]
    public void Renown_buys_a_town_discount_capped_at_a_quarter()
    {
        Assert.Equal(0.0, new RenownLog().Discount, 3);

        var rich = new RenownLog();
        rich.Sync(new RunStats
        {
            DeepestDepth = 20, BattlesWon = 50, MonstersSlain = 500, GoldEarned = 50000, Victory = true
        }, bestiaryDiscovered: 120, questsCompleted: 20);

        Assert.Equal(Achievements.TotalRenown, rich.Renown); // everything earned
        Assert.True(rich.Renown > 250);
        Assert.Equal(0.25, rich.Discount, 3); // capped
    }

    [Fact]
    public void Unlocked_achievements_round_trip_through_a_save()
    {
        var session = new GameSession(seed: 3);
        session.FillDefaultParty();
        session.Stats.DeepestDepth = 10;
        session.Stats.Victory = true;
        session.SyncAchievements();
        var renownBefore = session.Renown.Renown;
        Assert.True(renownBefore > 0);

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session));

        Assert.Equal(renownBefore, loaded.Renown.Renown);
        Assert.True(loaded.Renown.IsUnlocked("MANGAR"));
        Assert.True(loaded.Stats.Victory);
    }
}
