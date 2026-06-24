using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Items;
using BardsTale.Core.Persistence;
using BardsTale.Core.Quests;
using BardsTale.Core.Town;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

public class QuestTests
{
    private static Quest HuntSkeletons(int required = 3) => new()
    {
        Kind = QuestKind.Hunt,
        Giver = QuestGiver.TavernPatron,
        GiverName = "an old soldier",
        TurnInAt = TownBuilding.Tavern,
        TargetMonster = Bestiary.Skeleton.Name,
        TrophyName = "Bone Charm",
        Required = required,
        RewardGold = 100,
        RewardXp = 60
    };

    private static Encounter SlainGroup(MonsterTemplate t, int count) =>
        new(new[] { new MonsterGroup(t, count) });

    [Fact]
    public void Accepting_assigns_an_id_and_respects_the_cap()
    {
        var log = new QuestLog();
        for (var i = 0; i < QuestLog.MaxActive; i++)
            Assert.True(log.Accept(HuntSkeletons()));

        Assert.True(log.IsFull);
        Assert.False(log.Accept(HuntSkeletons())); // over the cap
        Assert.Equal(QuestLog.MaxActive, log.ActiveCount);
        Assert.All(log.Active, q => Assert.False(string.IsNullOrEmpty(q.Id)));
        Assert.Equal(log.Active.Select(q => q.Id).Distinct().Count(), log.ActiveCount); // ids unique
    }

    [Fact]
    public void Defeating_the_target_advances_progress_and_marks_ready()
    {
        var log = new QuestLog();
        var quest = HuntSkeletons(required: 3);
        log.Accept(quest);

        log.RecordVictory(SlainGroup(Bestiary.Skeleton, 2));
        Assert.Equal(2, quest.Current);
        Assert.Equal(QuestStatus.Active, quest.Status);

        log.RecordVictory(SlainGroup(Bestiary.Skeleton, 5)); // caps at the requirement
        Assert.Equal(3, quest.Current);
        Assert.Equal(QuestStatus.ReadyToTurnIn, quest.Status);
        Assert.Contains(quest, log.ReadyAt(TownBuilding.Tavern));
        Assert.Empty(log.ReadyAt(TownBuilding.Shop));
    }

    [Fact]
    public void Unrelated_kills_do_not_advance_a_quest()
    {
        var log = new QuestLog();
        var quest = HuntSkeletons();
        log.Accept(quest);

        log.RecordVictory(SlainGroup(Bestiary.GiantRat, 4));
        Assert.Equal(0, quest.Current);
        Assert.Equal(QuestStatus.Active, quest.Status);
    }

    [Fact]
    public void Claiming_pays_gold_xp_and_a_bonus_item_then_archives_the_quest()
    {
        var rng = new SystemRandomSource(seed: 7);
        var party = NewGame.CreateDefaultParty(rng);
        party.Gold = 0;
        var living = party.Members.Where(m => !m.IsDead).ToList();
        var xpBefore = living.Select(m => m.Experience).ToList();

        var log = new QuestLog();
        var quest = HuntSkeletons(required: 2);
        quest.RewardItem = Items.HealingPotion.Name;
        log.Accept(quest);
        log.RecordVictory(SlainGroup(Bestiary.Skeleton, 2));

        var line = log.Claim(quest, party);

        Assert.NotNull(line);
        Assert.Equal(100, party.Gold);
        Assert.Contains(Items.HealingPotion, party.Inventory);
        Assert.Empty(log.Active);
        Assert.Contains(quest, log.Completed);
        Assert.Equal(QuestStatus.Completed, quest.Status);
        // each living hero gained a share of the XP
        for (var i = 0; i < living.Count; i++)
            Assert.True(living[i].Experience > xpBefore[i]);
    }

    [Fact]
    public void Cannot_claim_a_quest_whose_objective_is_unmet()
    {
        var rng = new SystemRandomSource(seed: 1);
        var party = NewGame.CreateDefaultParty(rng);
        var log = new QuestLog();
        var quest = HuntSkeletons(required: 3);
        log.Accept(quest);
        log.RecordVictory(SlainGroup(Bestiary.Skeleton, 1)); // not done

        Assert.Null(log.Claim(quest, party));
        Assert.Contains(quest, log.Active);
    }

    [Fact]
    public void Abandoning_drops_a_quest_without_reward_or_archiving_it()
    {
        var log = new QuestLog();
        var keep = HuntSkeletons();
        var drop = HuntSkeletons();
        log.Accept(keep);
        log.Accept(drop);
        log.RecordVictory(SlainGroup(Bestiary.Skeleton, 1)); // some progress on both

        Assert.True(log.Abandon(drop));
        Assert.Equal(1, log.ActiveCount);
        Assert.Contains(keep, log.Active);
        Assert.DoesNotContain(drop, log.Active);
        Assert.DoesNotContain(drop, log.Completed); // abandoned, not finished
        Assert.False(log.Abandon(drop)); // already gone
    }

    [Fact]
    public void The_factory_builds_a_well_formed_quest_for_every_giver()
    {
        var rng = new SystemRandomSource(seed: 42);
        foreach (var giver in new[] { QuestGiver.Shopkeeper, QuestGiver.TavernPatron, QuestGiver.Stranger })
        {
            var quest = QuestFactory.Create(giver, rng, depth: 3);
            Assert.Equal(giver, quest.Giver);
            Assert.False(string.IsNullOrWhiteSpace(quest.TargetMonster));
            Assert.True(quest.Required >= 1);
            Assert.True(quest.RewardGold > 0);
            Assert.Equal(giver == QuestGiver.Shopkeeper ? TownBuilding.Shop : TownBuilding.Tavern, quest.TurnInAt);
            // a reward item (if any) must be resolvable on load
            if (quest.RewardItem is not null)
                Assert.NotNull(Items.Find(quest.RewardItem));
            Assert.False(string.IsNullOrWhiteSpace(quest.Title));
            Assert.False(string.IsNullOrWhiteSpace(quest.Pitch));
        }
    }

    [Fact]
    public void Quests_survive_a_save_load_round_trip()
    {
        var session = new GameSession(seed: 5);
        session.FillDefaultParty();

        var active = QuestFactory.Create(QuestGiver.Shopkeeper, session.Rng, depth: 2);
        session.Quests.Accept(active);
        session.Quests.RecordVictory(SlainGroup(
            Bestiary.Common.First(m => m.Name == active.TargetMonster), 1));

        var done = HuntSkeletons(required: 1);
        session.Quests.Accept(done);
        session.Quests.RecordVictory(SlainGroup(Bestiary.Skeleton, 1));
        session.Quests.Claim(done, session.Party);

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session));

        Assert.Single(loaded.Quests.Active);
        Assert.Single(loaded.Quests.Completed);
        var reloaded = loaded.Quests.Active[0];
        Assert.Equal(active.Id, reloaded.Id);
        Assert.Equal(active.TargetMonster, reloaded.TargetMonster);
        Assert.Equal(active.Current, reloaded.Current);
        Assert.Equal(active.Required, reloaded.Required);
        Assert.Equal(active.Kind, reloaded.Kind);
        Assert.Equal(session.Quests.NextId, loaded.Quests.NextId);
    }
}
