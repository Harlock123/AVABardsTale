using System.Text.Json;
using BardsTale.Core.Characters;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Game;
using BardsTale.Core.Geometry;
using BardsTale.Core.Items;
using BardsTale.Core.Lore;
using BardsTale.Core.Quests;
using BardsTale.Core.Town;
using ItemDb = BardsTale.Core.Items.Items;

namespace BardsTale.Core.Persistence;

/// <summary>Converts a <see cref="GameSession"/> to and from a JSON save file.</summary>
public static class GameSerializer
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string ToJson(GameSession session) => JsonSerializer.Serialize(ToData(session), Options);

    public static GameSession FromJson(string json)
    {
        var data = JsonSerializer.Deserialize<SaveData>(json)
                   ?? throw new InvalidDataException("Save file is empty or corrupt.");
        return FromData(data);
    }

    // --- session -> data ---

    public static SaveData ToData(GameSession session)
    {
        var p = session.Party;
        var data = new SaveData
        {
            Ascension = session.Ascension,
            Ironman = session.Ironman,
            Party = new PartySave
            {
                Gold = p.Gold,
                BankedGold = p.BankedGold,
                Keys = p.Keys,
                TownX = session.TownPosition.X,
                TownY = session.TownPosition.Y,
                TownFacing = (int)session.TownFacing,
                DungeonX = p.Position.X,
                DungeonY = p.Position.Y,
                DungeonFacing = (int)p.Facing,
                Inventory = p.Inventory
                    .Select(i => new ItemRefSave { Name = i.Name, Identified = i.Identified }).ToList(),
                Members = p.Members.Select(ToCharacterSave).ToList()
            }
        };

        if (session.ActiveDungeon is { } dungeon)
            data.Dungeon = ToDungeonSave(dungeon);

        data.Stats = new RunStatsSave
        {
            BattlesWon = session.Stats.BattlesWon,
            MonstersSlain = session.Stats.MonstersSlain,
            GoldEarned = session.Stats.GoldEarned,
            DeepestDepth = session.Stats.DeepestDepth,
            Victory = session.Stats.Victory
        };

        data.Renown = session.Renown.UnlockedIds.ToList();

        data.Quests = new QuestLogSave
        {
            NextId = session.Quests.NextId,
            Active = session.Quests.Active.Select(ToQuestSave).ToList(),
            Completed = session.Quests.Completed.Select(ToQuestSave).ToList()
        };

        data.Codex = new CodexSave
        {
            Entries = session.Codex.Entries.Values
                .Select(e => new CodexEntrySave { Name = e.Name, Slain = e.Slain, FirstSeenDepth = e.FirstSeenDepth })
                .ToList()
        };

        return data;
    }

    private static QuestSave ToQuestSave(Quest q) => new()
    {
        Id = q.Id,
        Kind = (int)q.Kind,
        Giver = (int)q.Giver,
        GiverName = q.GiverName,
        TurnInAt = (int)q.TurnInAt,
        TargetMonster = q.TargetMonster,
        TrophyName = q.TrophyName,
        Required = q.Required,
        Current = q.Current,
        RewardGold = q.RewardGold,
        RewardXp = q.RewardXp,
        RewardItem = q.RewardItem,
        Status = (int)q.Status
    };

    private static Quest FromQuestSave(QuestSave s) => new()
    {
        Id = s.Id,
        Kind = (QuestKind)s.Kind,
        Giver = (QuestGiver)s.Giver,
        GiverName = s.GiverName,
        TurnInAt = (TownBuilding)s.TurnInAt,
        TargetMonster = s.TargetMonster,
        TrophyName = s.TrophyName,
        Required = s.Required,
        Current = s.Current,
        RewardGold = s.RewardGold,
        RewardXp = s.RewardXp,
        RewardItem = s.RewardItem,
        Status = (QuestStatus)s.Status
    };

    private static CharacterSave ToCharacterSave(Character c) => new()
    {
        Name = c.Name,
        Race = (int)c.Race,
        Class = (int)c.Class,
        Level = c.Level,
        Experience = c.Experience,
        Gold = c.Gold,
        MaxHitPoints = c.MaxHitPoints,
        HitPoints = c.HitPoints,
        MaxSpellPoints = c.MaxSpellPoints,
        SpellPoints = c.SpellPoints,
        Strength = c.Attributes.Strength,
        Intelligence = c.Attributes.Intelligence,
        Dexterity = c.Attributes.Dexterity,
        Constitution = c.Attributes.Constitution,
        Luck = c.Attributes.Luck,
        Status = (int)c.Status,
        DrainedLevels = c.DrainedLevels,
        DrainedHitPoints = c.DrainedHitPoints,
        DrainedSpellPoints = c.DrainedSpellPoints,
        DrainedStrength = c.DrainedAttributes.Strength,
        DrainedIntelligence = c.DrainedAttributes.Intelligence,
        DrainedDexterity = c.DrainedAttributes.Dexterity,
        DrainedConstitution = c.DrainedAttributes.Constitution,
        DrainedLuck = c.DrainedAttributes.Luck,
        Weapon = c.Weapon?.Name,
        Armor = c.Armor?.Name,
        Shield = c.Shield?.Name,
        Ring1 = c.Ring1?.Name,
        Ring2 = c.Ring2?.Name,
        Amulet = c.Amulet?.Name,
        KnownSpells = c.KnownSpells.ToList(),
        KnownSongs = c.KnownSongs.ToList(),
        BardTunes = c.BardTunes
    };

    private static DungeonSave ToDungeonSave(GameState dungeon)
    {
        var save = new DungeonSave
        {
            Depth = dungeon.Depth,
            LightRemaining = dungeon.LightRemaining
        };
        foreach (var (depth, maze) in dungeon.Levels.OrderBy(kv => kv.Key))
            save.Levels.Add(ToLevelSave(depth, maze));
        return save;
    }

    private static LevelSave ToLevelSave(int depth, Maze maze)
    {
        var level = new LevelSave
        {
            Depth = depth,
            Name = maze.Name,
            Width = maze.Width,
            Height = maze.Height,
            StartX = maze.StartPosition.X,
            StartY = maze.StartPosition.Y,
            StartFacing = (int)maze.StartFacing
        };
        for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
            {
                var cell = maze[x, y];
                level.Cells.Add(new CellSave
                {
                    Walls = (int)cell.Walls,
                    Feature = (int)cell.Feature,
                    Text = cell.Text,
                    Visited = cell.Visited,
                    DestX = cell.Destination?.X,
                    DestY = cell.Destination?.Y,
                    SecretDoors = (int)cell.SecretDoors,
                    Gates = (int)cell.Gates,
                    LockedDoors = (int)cell.LockedDoors,
                    RiddleId = cell.RiddleId
                });
            }
        return level;
    }

    // --- data -> session ---

    public static GameSession FromData(SaveData data)
    {
        var session = new GameSession
        {
            Ascension = data.Ascension,
            Ironman = data.Ironman
        };
        var p = session.Party;

        p.Gold = data.Party.Gold;
        p.BankedGold = data.Party.BankedGold;
        p.Keys = data.Party.Keys;
        session.TownPosition = new Position(data.Party.TownX, data.Party.TownY);
        session.TownFacing = (Direction)data.Party.TownFacing;
        p.Position = new Position(data.Party.DungeonX, data.Party.DungeonY);
        p.Facing = (Direction)data.Party.DungeonFacing;

        foreach (var entry in data.Party.Inventory)
            if (ItemDb.Find(entry.Name) is { } item)
                p.Inventory.Add(entry.Identified ? item : item.AsUnidentified());

        foreach (var member in data.Party.Members)
            p.Add(FromCharacterSave(member));

        session.Stats.BattlesWon = data.Stats.BattlesWon;
        session.Stats.MonstersSlain = data.Stats.MonstersSlain;
        session.Stats.GoldEarned = data.Stats.GoldEarned;
        session.Stats.DeepestDepth = data.Stats.DeepestDepth;
        session.Stats.Victory = data.Stats.Victory;

        foreach (var id in data.Renown)
            session.Renown.Restore(id);

        foreach (var quest in data.Quests.Active)
            session.Quests.RestoreActive(FromQuestSave(quest));
        foreach (var quest in data.Quests.Completed)
            session.Quests.RestoreCompleted(FromQuestSave(quest));
        session.Quests.NextId = Math.Max(1, data.Quests.NextId);

        foreach (var entry in data.Codex.Entries)
            session.Codex.Restore(new CodexEntry
            {
                Name = entry.Name, Slain = entry.Slain, FirstSeenDepth = entry.FirstSeenDepth
            });

        if (data.Dungeon is { } dungeonSave && dungeonSave.Levels.Count > 0)
        {
            var current = dungeonSave.Levels.FirstOrDefault(l => l.Depth == dungeonSave.Depth)
                          ?? dungeonSave.Levels[0];
            var game = new GameState(p, FromLevelSave(current), session.Rng,
                dungeonSave.Depth, p.Position, p.Facing, dungeonSave.LightRemaining, session.Ascension);
            foreach (var level in dungeonSave.Levels)
                if (level.Depth != current.Depth)
                    game.AddLevel(level.Depth, FromLevelSave(level));
            session.RestoreDungeon(game);
        }

        return session;
    }

    private static Character FromCharacterSave(CharacterSave s)
    {
        var c = new Character
        {
            Name = s.Name,
            Race = (Race)s.Race,
            Class = (CharacterClass)s.Class,
            Attributes = new AttributeSet
            {
                Strength = s.Strength,
                Intelligence = s.Intelligence,
                Dexterity = s.Dexterity,
                Constitution = s.Constitution,
                Luck = s.Luck
            },
            Level = s.Level,
            Experience = s.Experience,
            Gold = s.Gold,
            MaxHitPoints = s.MaxHitPoints,
            HitPoints = s.HitPoints,
            MaxSpellPoints = s.MaxSpellPoints,
            SpellPoints = s.SpellPoints,
            Status = (StatusEffect)s.Status,
            DrainedLevels = s.DrainedLevels,
            DrainedHitPoints = s.DrainedHitPoints,
            DrainedSpellPoints = s.DrainedSpellPoints,
            Weapon = ItemDb.Find(s.Weapon),
            Armor = ItemDb.Find(s.Armor),
            Shield = ItemDb.Find(s.Shield),
            Ring1 = ItemDb.Find(s.Ring1),
            Ring2 = ItemDb.Find(s.Ring2),
            Amulet = ItemDb.Find(s.Amulet)
        };
        // Older saves stored a single accessory; slot it where it now belongs.
        if (ItemDb.Find(s.Accessory) is { } legacy)
        {
            if (legacy.Slot == ItemSlot.Amulet) c.Amulet ??= legacy;
            else if (c.Ring1 is null) c.Ring1 = legacy;
            else c.Ring2 ??= legacy;
        }
        c.KnownSpells.AddRange(s.KnownSpells);
        c.KnownSongs.AddRange(s.KnownSongs);
        // Older saves predate bardic tunes; give bards a full repertoire rather than zero.
        c.BardTunes = c.IsBard ? (s.BardTunes > 0 ? s.BardTunes : c.MaxBardTunes) : 0;
        c.DrainedAttributes.Strength = s.DrainedStrength;
        c.DrainedAttributes.Intelligence = s.DrainedIntelligence;
        c.DrainedAttributes.Dexterity = s.DrainedDexterity;
        c.DrainedAttributes.Constitution = s.DrainedConstitution;
        c.DrainedAttributes.Luck = s.DrainedLuck;
        return c;
    }

    private static Maze FromLevelSave(LevelSave d)
    {
        var maze = new Maze(d.Name, d.Width, d.Height)
        {
            StartPosition = new Position(d.StartX, d.StartY),
            StartFacing = (Direction)d.StartFacing
        };
        for (var y = 0; y < d.Height; y++)
            for (var x = 0; x < d.Width; x++)
            {
                var cs = d.Cells[y * d.Width + x];
                var cell = maze[x, y];
                cell.Walls = (Walls)cs.Walls;
                cell.Feature = (CellFeature)cs.Feature;
                cell.Text = cs.Text;
                cell.Visited = cs.Visited;
                cell.SecretDoors = (Walls)cs.SecretDoors;
                cell.Gates = (Walls)cs.Gates;
                cell.LockedDoors = (Walls)cs.LockedDoors;
                cell.RiddleId = cs.RiddleId;
                if (cs.DestX is { } dx && cs.DestY is { } dy)
                    cell.Destination = new Position(dx, dy);
            }
        return maze;
    }
}
