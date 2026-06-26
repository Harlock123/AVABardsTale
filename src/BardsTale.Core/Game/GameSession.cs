using BardsTale.Core.Characters;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Geometry;
using BardsTale.Core.Items;
using BardsTale.Core.Lore;
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

    /// <summary>
    /// New Game+ level: 0 on a first playthrough, +1 each time the party carries over after a win.
    /// Every encounter and boss is scaled up by this (see <see cref="Combat.NgPlus"/>).
    /// </summary>
    public int Ascension { get; set; }

    /// <summary>
    /// Ironman (permadeath) run: a total party kill ends the game for good and wipes the save,
    /// and manual save/load is disabled so the single autosave can't be reloaded to cheat death.
    /// </summary>
    public bool Ironman { get; set; }

    /// <summary>The run's chosen challenge level, scaling foes, ambushes, camp risk and rewards.</summary>
    public Combat.Difficulty Difficulty { get; set; } = Combat.Difficulty.Normal;

    /// <summary>The daily-challenge seed this run was started on, or null for an ordinary run.</summary>
    public int? ChallengeSeed { get; set; }

    /// <summary>True when this run is a seeded daily challenge (scored, fixed party, played to the death).</summary>
    public bool IsChallenge => ChallengeSeed.HasValue;

    /// <summary>Running tally of the party's deeds, shown on the victory screen.</summary>
    public RunStats Stats { get; } = new();

    /// <summary>The party's side-quest journal — quests offered by townsfolk and their progress.</summary>
    public QuestLog Quests { get; } = new();

    /// <summary>The town notice board — a rotating set of quests to pick up. Transient (not saved).</summary>
    public QuestBoard QuestBoard { get; } = new();

    /// <summary>The bestiary — which monsters the party has faced and how many they've slain.</summary>
    public MonsterCodex Codex { get; } = new();

    /// <summary>Achievements unlocked and the renown (and town discount) they earn.</summary>
    public RenownLog Renown { get; } = new();

    /// <summary>Unlocks any newly-earned achievements from the current run, returning the new ones.</summary>
    public IReadOnlyList<Achievement> SyncAchievements()
        => Renown.Sync(Stats, Codex.DiscoveredCount, Quests.Completed.Count);

    /// <summary>The walkable Skara Brae overworld, plus the party's persisted position in it.</summary>
    public TownMap Town { get; }
    public Position TownPosition { get; set; }
    public Direction TownFacing { get; set; }

    /// <summary>
    /// The wares for sale at Garth's Equipment Shoppe — restocked from progress: Garth carries
    /// stronger accessories the deeper the party has reached. Re-read each town visit.
    /// </summary>
    public IReadOnlyList<Item> ShopStock => Items.ShopWares.For(Stats.DeepestDepth);

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
            _dungeon = new GameState(Party, maze, Rng, Ascension, Combat.DifficultyProfile.For(Difficulty));
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

    /// <summary>
    /// Begins a New Game+ run: a fresh session that carries this party forward — their levels,
    /// gear, gold and stash intact and fully rested — into a tougher world (Ascension +1). The
    /// dungeon, run tally, quests and town progress reset; the Ironman flag carries over.
    /// </summary>
    public GameSession StartNewGamePlus()
    {
        var ng = new GameSession
        {
            Ascension = Ascension + 1,
            Ironman = Ironman,
            Difficulty = Difficulty
        };
        foreach (var m in Party.Members.ToList())
        {
            m.CureAilments();
            m.FullHeal();
            m.RefreshBardTunes();
            ng.Party.Add(m);
        }
        ng.Party.Gold = Party.Gold;
        ng.Party.BankedGold = Party.BankedGold;
        ng.Party.Inventory.AddRange(Party.Inventory);
        return ng;
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
