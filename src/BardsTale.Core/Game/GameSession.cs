using BardsTale.Core.Characters;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Geometry;
using BardsTale.Core.Items;
using BardsTale.Core.Quests;
using BardsTale.Core.Town;
using BardsTale.Core.Util;

namespace BardsTale.Core.Game;

/// <summary>
/// The whole save: the persistent party and shared randomness, the town economy,
/// and the (lazily created) dungeon the party explores. Survives trips between
/// town and the catacombs.
/// </summary>
public sealed class GameSession
{
    private GameState? _dungeon;

    public GameSession(int? seed = null)
    {
        Rng = new SystemRandomSource(seed);
        Party = new Party();
        Factory = new CharacterFactory(Rng);
        Town = TownMapBuilder.Build();
        TownPosition = Town.StartPosition;
        TownFacing = Town.StartFacing;
    }

    public IRandomSource Rng { get; }
    public Party Party { get; }
    public CharacterFactory Factory { get; }

    /// <summary>Running tally of the party's deeds, shown on the victory screen.</summary>
    public RunStats Stats { get; } = new();

    /// <summary>The party's side-quest journal — quests offered by townsfolk and their progress.</summary>
    public QuestLog Quests { get; } = new();

    /// <summary>The walkable Skara Brae overworld, plus the party's persisted position in it.</summary>
    public TownMap Town { get; }
    public Position TownPosition { get; set; }
    public Direction TownFacing { get; set; }

    /// <summary>The wares for sale at Garth's Equipment Shoppe.</summary>
    public IReadOnlyList<Item> ShopStock { get; } = new[]
    {
        Items.Items.Dagger, Items.Items.ShortSword, Items.Items.LongSword, Items.Items.BattleAxe,
        Items.Items.Staff, Items.Items.LeatherArmor, Items.Items.ChainMail, Items.Items.PlateMail,
        Items.Items.SmallShield, Items.Items.Robes,
        Items.Items.HealingPotion, Items.Items.ManaDraught, Items.Items.Antidote, Items.Items.ResurrectionDust
    };

    public bool HasActiveDungeon => _dungeon is not null;

    /// <summary>The currently-explored dungeon, if the party has descended. Null while purely in town.</summary>
    public GameState? ActiveDungeon => _dungeon;

    /// <summary>Reinstates a dungeon restored from a saved game.</summary>
    public void RestoreDungeon(GameState dungeon) => _dungeon = dungeon;

    /// <summary>Enters the catacombs, building level 1 the first time and resuming thereafter.</summary>
    public GameState EnterDungeon()
    {
        if (_dungeon is null)
        {
            var maze = new MazeBuilder(Rng).Build("Catacombs — Level 1", 16, 16);
            _dungeon = new GameState(Party, maze, Rng);
        }
        else
        {
            _dungeon.ReturnToEntrance();
        }
        return _dungeon;
    }

    /// <summary>Fills the party with the classic ready-made adventurers, up to six.</summary>
    public void FillDefaultParty()
    {
        foreach (var c in NewGame.CreateDefaultParty(Rng).Members)
        {
            if (Party.Members.Count >= Party.MaxSize) break;
            JoinParty(c);
        }
    }

    /// <summary>Adds a created character to the party, pooling their starting gold into the purse.</summary>
    public bool JoinParty(Character c)
    {
        if (!Party.Add(c)) return false;
        Party.Gold += c.Gold;
        c.Gold = 0;
        return true;
    }
}
