using System.Collections.Generic;
using System.Linq;
using BardsTale.Core.Combat;
using BardsTale.Core.Town;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

/// <summary>
/// Covers dynamic tavern rumours: bought rounds turn up real, progress-aware tips — the next
/// floor's boss, what prowls there, and the secrets/locked vaults still waiting below.
/// </summary>
public class TavernRumorTests
{
    private const string Tavern = "The Scarlet Bard";

    // Collects many rounds so the (randomised) rumour pool is well sampled.
    private static List<string> ManyRounds(RumorContext? ctx, int count = 300)
    {
        var rng = new SystemRandomSource(seed: 7);
        var heard = new List<string>();
        for (var i = 0; i < count; i++)
            heard.Add(Taverns.Rumor(Tavern, rng, ctx));
        return heard;
    }

    [Fact]
    public void A_round_can_name_the_boss_on_the_next_floor_down()
    {
        var ctx = new RumorContext(DeepestDepth: 1, SecretDoors: 0, Riddles: 0, Gates: 0, LockedDoors: 0, Keys: 0);
        var nextBoss = Bosses.BossForDepth(2).Name;

        Assert.Contains(ManyRounds(ctx), r => r.Contains(nextBoss));
    }

    [Fact]
    public void Near_the_bottom_the_rumors_warn_of_Mangar()
    {
        var ctx = new RumorContext(Bosses.FinalDepth, 0, 0, 0, 0, 0);
        Assert.Contains(ManyRounds(ctx), r => r.Contains("Mangar"));
    }

    [Fact]
    public void Unfound_secrets_prompt_a_search_hint()
    {
        var withSecrets = new RumorContext(2, SecretDoors: 3, Riddles: 1, Gates: 1, LockedDoors: 0, Keys: 0);
        var heard = ManyRounds(withSecrets);

        Assert.Contains(heard, r => r.Contains("search"));   // hidden passages
        Assert.Contains(heard, r => r.Contains("riddle"));   // riddle stone
        Assert.Contains(heard, r => r.Contains("lever"));    // barred-gate vault
    }

    [Fact]
    public void The_locked_door_hint_reflects_whether_a_key_is_carried()
    {
        var noKey = ManyRounds(new RumorContext(2, 0, 0, 0, LockedDoors: 1, Keys: 0));
        Assert.Contains(noKey, r => r.Contains("iron key lies dropped"));
        Assert.DoesNotContain(noKey, r => r.Contains("Carrying an iron key"));

        var withKey = ManyRounds(new RumorContext(2, 0, 0, 0, LockedDoors: 1, Keys: 1));
        Assert.Contains(withKey, r => r.Contains("Carrying an iron key"));
        Assert.DoesNotContain(withKey, r => r.Contains("iron key lies dropped"));
    }

    [Fact]
    public void A_round_without_context_still_gives_a_flavour_rumor()
    {
        var rng = new SystemRandomSource(seed: 1);
        var rumor = Taverns.RandomRumor(Tavern, rng);
        Assert.False(string.IsNullOrWhiteSpace(rumor));
    }

    [Fact]
    public void An_unknown_tavern_with_context_still_offers_a_tip()
    {
        var rng = new SystemRandomSource(seed: 1);
        var ctx = new RumorContext(1, 0, 0, 0, 0, 0);
        var rumor = Taverns.Rumor("Nowhere Inn", rng, ctx); // no flavour pool → must fall through to a tip

        Assert.NotEqual("The regulars have nothing to say tonight.", rumor);
    }
}
