using BardsTale.Core.Combat;
using BardsTale.Core.Town;
using BardsTale.Core.Util;
using ItemDb = BardsTale.Core.Items.Items;

namespace BardsTale.Core.Quests;

/// <summary>
/// Rolls fresh side quests for the town's quest givers. Each giver favours a kind:
/// Garth wants trophies (Collect), tavern regulars want vermin culled (Hunt), and
/// strangers want a lost heirloom recovered (Retrieve). Rewards scale with the
/// target's worth, the amount asked, and how deep the party has been.
/// </summary>
public static class QuestFactory
{
    private static readonly IReadOnlyDictionary<string, string> Trophies = new Dictionary<string, string>
    {
        ["Skeleton"] = "Bone Charm",
        ["Giant Rat"] = "Rat Tail",
        ["Kobold"] = "Kobold Fang",
        ["Giant Spider"] = "Spider Venom Gland",
        ["Coven Witch"] = "Witch's Talisman",
        ["Spark Imp"] = "Imp Horn",
        ["Cutpurse"] = "Stolen Locket"
    };

    private static readonly string[] Relics =
    {
        "Silver Chalice", "Moonstone Amulet", "Ancient Signet Ring",
        "Jeweled Dagger", "Crystal Skull", "Gilded Psalter", "Obsidian Idol"
    };

    private static readonly string[] Patrons =
    {
        "a grizzled mercenary", "an old soldier", "the barkeep",
        "a nervous merchant", "a one-eyed sailor"
    };

    private static readonly string[] Strangers =
    {
        "a hooded stranger", "a frightened scholar", "a limping beggar",
        "a cloaked widow", "a wide-eyed urchin"
    };

    /// <summary>Builds (but does not accept) a quest from the given giver, tuned to <paramref name="depth"/>.</summary>
    public static Quest Create(QuestGiver giver, IRandomSource rng, int depth)
    {
        depth = Math.Max(1, depth);
        return giver switch
        {
            QuestGiver.Shopkeeper => CreateCollect(rng, depth),
            QuestGiver.TavernPatron => CreateHunt(rng, depth),
            _ => CreateRetrieve(rng, depth)
        };
    }

    private static Quest CreateHunt(IRandomSource rng, int depth)
    {
        var monster = rng.Pick(Bestiary.Common);
        var count = rng.Next(4, 9);
        return new Quest
        {
            Kind = QuestKind.Hunt,
            Giver = QuestGiver.TavernPatron,
            GiverName = rng.Pick(Patrons),
            TurnInAt = TownBuilding.Tavern,
            TargetMonster = monster.Name,
            TrophyName = TrophyFor(monster.Name),
            Required = count,
            RewardGold = count * (8 + monster.GoldValue) + 15 * depth,
            RewardXp = count * (monster.ExperienceValue / 2 + 5)
        };
    }

    private static Quest CreateCollect(IRandomSource rng, int depth)
    {
        var monster = rng.Pick(Bestiary.Common);
        var count = rng.Next(3, 7);
        var quest = new Quest
        {
            Kind = QuestKind.Collect,
            Giver = QuestGiver.Shopkeeper,
            GiverName = "Garth",
            TurnInAt = TownBuilding.Shop,
            TargetMonster = monster.Name,
            TrophyName = TrophyFor(monster.Name),
            Required = count,
            RewardGold = count * (12 + monster.GoldValue) + 20 * depth,
            RewardXp = count * (monster.ExperienceValue / 2 + 8)
        };
        if (rng.Chance(0.40))
            quest.RewardItem = rng.Pick(Consumables);
        return quest;
    }

    private static Quest CreateRetrieve(IRandomSource rng, int depth)
    {
        var monster = rng.Pick(Bestiary.Tough);
        return new Quest
        {
            Kind = QuestKind.Retrieve,
            Giver = QuestGiver.Stranger,
            GiverName = rng.Pick(Strangers),
            TurnInAt = TownBuilding.Tavern,
            TargetMonster = monster.Name,
            TrophyName = rng.Pick(Relics),
            Required = 1,
            RewardGold = 120 + monster.GoldValue * 2 + 35 * depth,
            RewardXp = monster.ExperienceValue * 2 + 40,
            RewardItem = ItemDb.ResurrectionDust.Name
        };
    }

    private static readonly string[] Consumables =
    {
        ItemDb.HealingPotion.Name, ItemDb.ManaDraught.Name, ItemDb.Antidote.Name
    };

    private static string TrophyFor(string monster) =>
        Trophies.TryGetValue(monster, out var t) ? t : $"{monster} Trophy";
}
