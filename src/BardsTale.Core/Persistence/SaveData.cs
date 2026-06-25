namespace BardsTale.Core.Persistence;

// Plain serializable snapshots of game state. Kept deliberately flat and
// independent of the domain types so the on-disk format is stable and explicit.

public sealed class SaveData
{
    public int Version { get; set; } = 1;
    public PartySave Party { get; set; } = new();
    public DungeonSave? Dungeon { get; set; }
    public RunStatsSave Stats { get; set; } = new();
    public QuestLogSave Quests { get; set; } = new();
    public CodexSave Codex { get; set; } = new();

    /// <summary>Unlocked achievement ids (renown is derived from these).</summary>
    public List<string> Renown { get; set; } = new();
}

/// <summary>The bestiary: which monsters have been faced, and how many slain.</summary>
public sealed class CodexSave
{
    public List<CodexEntrySave> Entries { get; set; } = new();
}

public sealed class CodexEntrySave
{
    public string Name { get; set; } = "";
    public int Slain { get; set; }
    public int FirstSeenDepth { get; set; } = 1;
}

/// <summary>The side-quest journal: in-progress quests, the archive, and the next id to hand out.</summary>
public sealed class QuestLogSave
{
    public int NextId { get; set; } = 1;
    public List<QuestSave> Active { get; set; } = new();
    public List<QuestSave> Completed { get; set; } = new();
}

public sealed class QuestSave
{
    public string Id { get; set; } = "";
    public int Kind { get; set; }
    public int Giver { get; set; }
    public string GiverName { get; set; } = "";
    public int TurnInAt { get; set; }
    public string TargetMonster { get; set; } = "";
    public string TrophyName { get; set; } = "";
    public int Required { get; set; }
    public int Current { get; set; }
    public int RewardGold { get; set; }
    public int RewardXp { get; set; }
    public string? RewardItem { get; set; }
    public int Status { get; set; }
}

public sealed class RunStatsSave
{
    public int BattlesWon { get; set; }
    public int MonstersSlain { get; set; }
    public int GoldEarned { get; set; }
    public int DeepestDepth { get; set; } = 1;
    public bool Victory { get; set; }
}

public sealed class PartySave
{
    public int Gold { get; set; }
    public int BankedGold { get; set; }
    public int TownX { get; set; }
    public int TownY { get; set; }
    public int TownFacing { get; set; }
    public int DungeonX { get; set; }
    public int DungeonY { get; set; }
    public int DungeonFacing { get; set; }
    public List<CharacterSave> Members { get; set; } = new();
    public List<ItemRefSave> Inventory { get; set; } = new();
}

/// <summary>A stashed item: its true name, plus whether the party has identified it.</summary>
public sealed class ItemRefSave
{
    public string Name { get; set; } = "";
    public bool Identified { get; set; } = true;
}

public sealed class CharacterSave
{
    public string Name { get; set; } = "";
    public int Race { get; set; }
    public int Class { get; set; }
    public int Level { get; set; }
    public long Experience { get; set; }
    public int Gold { get; set; }
    public int MaxHitPoints { get; set; }
    public int HitPoints { get; set; }
    public int MaxSpellPoints { get; set; }
    public int SpellPoints { get; set; }
    public int Strength { get; set; }
    public int Intelligence { get; set; }
    public int Dexterity { get; set; }
    public int Constitution { get; set; }
    public int Luck { get; set; }
    public int Status { get; set; }
    public int DrainedLevels { get; set; }
    public int DrainedHitPoints { get; set; }
    public int DrainedSpellPoints { get; set; }
    public int DrainedStrength { get; set; }
    public int DrainedIntelligence { get; set; }
    public int DrainedDexterity { get; set; }
    public int DrainedConstitution { get; set; }
    public int DrainedLuck { get; set; }
    public string? Weapon { get; set; }
    public string? Armor { get; set; }
    public string? Shield { get; set; }
    public string? Ring1 { get; set; }
    public string? Ring2 { get; set; }
    public string? Amulet { get; set; }
    /// <summary>Legacy single-accessory field from older saves; loaded into a ring/amulet slot.</summary>
    public string? Accessory { get; set; }
    public List<string> KnownSpells { get; set; } = new();
    public List<string> KnownSongs { get; set; } = new();
}

public sealed class DungeonSave
{
    public int Depth { get; set; }          // the level the party currently stands on
    public int LightRemaining { get; set; }
    public List<LevelSave> Levels { get; set; } = new(); // every explored level, one per depth
}

/// <summary>A single explored dungeon level: its layout and revealed map.</summary>
public sealed class LevelSave
{
    public int Depth { get; set; }
    public string Name { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public int StartX { get; set; }
    public int StartY { get; set; }
    public int StartFacing { get; set; }
    public List<CellSave> Cells { get; set; } = new(); // row-major: index = y * Width + x
}

public sealed class CellSave
{
    public int Walls { get; set; }
    public int Feature { get; set; }
    public string? Text { get; set; }
    public bool Visited { get; set; }
    public int? DestX { get; set; }
    public int? DestY { get; set; }
}
