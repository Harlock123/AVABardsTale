using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Dungeon;
using BardsTale.Core.Geometry;
using BardsTale.Core.Items;
using BardsTale.Core.Util;

namespace BardsTale.Core.Game;

public enum MoveResultKind
{
    Moved,
    BlockedByWall,
    Turned,
    Encounter,
    Message,
    StairsDown,
    StairsUp,
    Exit,
    Spun,
    Teleported,
    Trapped,
    Darkness,
    AntiMagic,
    Chest,
    Riddle,
    Lever,
    KeyFound,
    Unlocked
}

public sealed record MoveResult(MoveResultKind Kind, string Description, Encounter? Encounter = null);

/// <summary>
/// The outcome of opening a treasure chest: narration, the spoils, and whether a trap went off.
/// When the "chest" was a disguised <see cref="Mimic"/>, the loot is empty and combat begins instead.
/// </summary>
public sealed record ChestResult(IReadOnlyList<string> Log, IReadOnlyList<Item> Loot, bool TrapSprang,
    Encounter? Mimic = null);

/// <summary>The outcome of making camp: whether the party rested, narration, and any ambush that interrupted it.</summary>
public sealed record CampResult(bool Rested, IReadOnlyList<string> Log, Encounter? Ambush);

/// <summary>The outcome of searching a cell for hidden passages.</summary>
public sealed record SearchResult(bool Found, string Message);

/// <summary>The outcome of answering a riddle tile.</summary>
public sealed record RiddleResult(bool Correct, IReadOnlyList<string> Log);

/// <summary>The outcome of pulling a rune lever: how many gates it raised, and narration.</summary>
public sealed record LeverResult(int GatesOpened, string Message);

/// <summary>
/// The live game world: the party exploring the current maze level. Owns movement,
/// wandering-monster checks and post-combat rewards.
/// </summary>
public sealed class GameState
{
    private readonly IRandomSource _rng;
    private readonly EncounterFactory _encounters;
    private readonly int _ascension; // New Game+ level — scales every encounter and boss
    private readonly DifficultyProfile _difficulty; // chosen challenge level — scales foes, ambushes and rewards
    private int _stepsSinceEncounter;

    // Each depth keeps its own maze, so a level's layout and explored map persist
    // when you climb away and return. Keyed by depth.
    private readonly Dictionary<int, Maze> _levels = new();

    public GameState(Party party, Maze maze, IRandomSource rng, int ascension = 0, DifficultyProfile? difficulty = null)
    {
        Party = party;
        Maze = maze;
        _rng = rng;
        _ascension = ascension;
        _difficulty = difficulty ?? DifficultyProfile.Normal;
        _encounters = new EncounterFactory(rng, ascension, _difficulty);
        _levels[Depth] = maze;
        Party.Position = maze.StartPosition;
        Party.Facing = maze.StartFacing;
        MarkVisited();
    }

    /// <summary>Restores a dungeon from a saved game, preserving depth, position and the explored map.</summary>
    public GameState(Party party, Maze maze, IRandomSource rng, int depth, Position position, Direction facing,
        int lightRemaining = 0, int ascension = 0, DifficultyProfile? difficulty = null)
    {
        Party = party;
        Maze = maze;
        _rng = rng;
        _ascension = ascension;
        _difficulty = difficulty ?? DifficultyProfile.Normal;
        _encounters = new EncounterFactory(rng, ascension, _difficulty);
        Depth = depth;
        _levels[Depth] = maze;
        Party.Position = position;
        Party.Facing = facing;
        LightRemaining = lightRemaining;
    }

    public Party Party { get; }
    public Maze Maze { get; private set; }
    public int Depth { get; private set; } = 1;

    /// <summary>Every explored level keyed by depth — used by save/load to persist each map.</summary>
    public IReadOnlyDictionary<int, Maze> Levels => _levels;

    /// <summary>Restores a previously-explored level into the depth map (used when loading a save).</summary>
    public void AddLevel(int depth, Maze maze) => _levels[depth] = maze;

    /// <summary>True when there is a level above the current one to climb back to.</summary>
    public bool CanAscend => Depth > 1;

    /// <summary>How many more steps a conjured light lasts. While lit, darkness is seen and mapped.</summary>
    public const int LightDurationSteps = 60;
    public int LightRemaining { get; private set; }
    public bool HasLight => LightRemaining > 0;

    /// <summary>Activates (or refreshes) magical light for the given number of steps.</summary>
    public void GrantLight(int steps) => LightRemaining = Math.Max(LightRemaining, steps);

    /// <summary>Shared randomness source, reused by combat so a seeded game is fully deterministic.</summary>
    public IRandomSource Rng => _rng;

    public Cell CurrentCell => Maze[Party.Position];

    /// <summary>True when the party stands in an anti-magic zone, where spells and songs fail.</summary>
    public bool MagicSuppressed => CurrentCell.Feature == CellFeature.AntiMagic;

    public MoveResult TurnLeft()
    {
        Party.Facing = Party.Facing.TurnLeft();
        return new MoveResult(MoveResultKind.Turned, $"You turn to face {Party.Facing}.");
    }

    public MoveResult TurnRight()
    {
        Party.Facing = Party.Facing.TurnRight();
        return new MoveResult(MoveResultKind.Turned, $"You turn to face {Party.Facing}.");
    }

    public MoveResult StepForward() => Step(Party.Facing);

    public MoveResult StepBackward() => Step(Party.Facing.Opposite());

    private MoveResult Step(Direction dir)
    {
        if (!Maze.CanMove(Party.Position, dir))
        {
            if (CurrentCell.HasGate(dir))
                return new MoveResult(MoveResultKind.BlockedByWall,
                    "An iron portcullis bars the way — some lever must raise it.");

            if (CurrentCell.HasLockedDoor(dir))
            {
                if (Party.Keys <= 0)
                    return new MoveResult(MoveResultKind.BlockedByWall,
                        "A locked door bars the way — you'll need to find a key.");
                // Spend a key to open the door; the party then steps through on the next move.
                Party.Keys--;
                Maze.OpenLockedDoor(Party.Position, dir);
                return new MoveResult(MoveResultKind.Unlocked,
                    "You fit an iron key to the lock — it turns with a clunk and the door swings open.");
            }

            return new MoveResult(MoveResultKind.BlockedByWall, "A wall blocks your way.");
        }

        Party.Position = Party.Position.Step(dir);
        MarkVisited();
        if (LightRemaining > 0) LightRemaining--;

        var cell = CurrentCell;
        switch (cell.Feature)
        {
            case CellFeature.Message when cell.Text is not null:
                return new MoveResult(MoveResultKind.Message, cell.Text);
            case CellFeature.StairsDown:
                return new MoveResult(MoveResultKind.StairsDown, "A stairway descends into darkness.");
            case CellFeature.StairsUp:
                return new MoveResult(MoveResultKind.StairsUp, "Stairs lead back up.");
            case CellFeature.Exit:
                return new MoveResult(MoveResultKind.Exit, "Sunlight ahead — the way out!");
            case CellFeature.SpinnerTrap:
                Party.Facing = (Direction)_rng.Next(0, 4);
                return new MoveResult(MoveResultKind.Spun,
                    $"The floor spins beneath you! You are now facing {Party.Facing}.");
            case CellFeature.Teleporter when cell.Destination is { } dest:
                Party.Position = dest;
                MarkVisited();
                return new MoveResult(MoveResultKind.Teleported, "Reality folds — you are wrenched elsewhere!");
            case CellFeature.Trap:
                return SpringTrap();
            case CellFeature.Darkness when !HasLight:
                return new MoveResult(MoveResultKind.Darkness, "It is pitch black here — you can see nothing.");
            case CellFeature.AntiMagic:
                return new MoveResult(MoveResultKind.AntiMagic,
                    "A dead, magicless silence presses in — spells and songs will not work here.");
            case CellFeature.BossLair:
                return new MoveResult(MoveResultKind.Encounter,
                    $"A monstrous presence rises to bar your way — the {Bosses.BossForDepth(Depth).Name}!",
                    Bosses.Create(Depth, _ascension, _difficulty));
            case CellFeature.Chest:
                return new MoveResult(MoveResultKind.Chest,
                    "A heavy treasure chest sits here, its lid latched shut.");
            case CellFeature.OrnateChest:
                return new MoveResult(MoveResultKind.Chest,
                    "An ornate, gold-filigreed chest rests here — clearly valuable, and surely trapped.");
            case CellFeature.Riddle:
                return new MoveResult(MoveResultKind.Riddle,
                    $"Glowing runes are graven in the floor: \"{Riddles.Get(cell.RiddleId).Question}\"");
            case CellFeature.Lever:
                return new MoveResult(MoveResultKind.Lever,
                    "A heavy rune-etched lever juts from the wall, begging to be pulled.");
            case CellFeature.Key:
                Party.Keys++;
                cell.Feature = CellFeature.None; // pocketed
                return new MoveResult(MoveResultKind.KeyFound,
                    "Half-buried in the dust lies an iron key — you pocket it.");
        }

        if (CheckForEncounter(out var encounter))
            return new MoveResult(MoveResultKind.Encounter,
                encounter!.HasElite ? "An elite foe leads monsters to block your path!" : "Monsters block your path!",
                encounter);

        return new MoveResult(MoveResultKind.Moved, DescribeView());
    }

    /// <summary>Marks the current boss lair as cleared so its fight does not recur.</summary>
    public void ClearBoss()
    {
        if (CurrentCell.Feature == CellFeature.BossLair)
            CurrentCell.Feature = CellFeature.None;
    }

    /// <summary>
    /// Searches the current cell for hidden doors. A Rogue greatly improves the odds; on success
    /// any secret door bordering the cell swings open, revealing the passage beyond.
    /// </summary>
    public SearchResult Search()
    {
        var secrets = Maze.SecretDoorsAt(Party.Position);
        if (secrets.Count == 0)
            return new SearchResult(false, "You search the stonework but find nothing hidden here.");

        var rogue = Party.Members
            .Where(m => !m.IsDead && m.Class == CharacterClass.Rogue)
            .OrderByDescending(m => m.Level)
            .FirstOrDefault();
        var chance = rogue is null ? 0.45 : Math.Min(0.95, 0.6 + 0.04 * rogue.Level);
        if (!_rng.Chance(chance))
            return new SearchResult(false, "Your search turns up nothing — though something here feels amiss. (Search again.)");

        foreach (var d in secrets)
            Maze.OpenSecretDoor(Party.Position, d);
        MarkVisited();
        var where = string.Join(" and ", secrets.Select(d => d.ToString().ToLowerInvariant()));
        return new SearchResult(true, $"You find a hidden door to the {where}!");
    }

    /// <summary>
    /// A Rogue's passive perception as the party moves: a small chance to instinctively notice a
    /// secret door bordering the cell just entered. Returns a message if one was found, else null.
    /// </summary>
    public string? RoguePassiveSearch()
    {
        var secrets = Maze.SecretDoorsAt(Party.Position);
        if (secrets.Count == 0) return null;

        var rogue = Party.Members
            .Where(m => !m.IsDead && m.Class == CharacterClass.Rogue)
            .OrderByDescending(m => m.Level)
            .FirstOrDefault();
        if (rogue is null) return null;

        var chance = Math.Min(0.40, 0.08 + 0.015 * rogue.Level); // far less reliable than a deliberate search
        if (!_rng.Chance(chance)) return null;

        foreach (var d in secrets)
            Maze.OpenSecretDoor(Party.Position, d);
        var where = string.Join(" and ", secrets.Select(d => d.ToString().ToLowerInvariant()));
        return $"{rogue.Name} instinctively notices a hidden door to the {where}!";
    }

    /// <summary>Undiscovered secret doors plus unsolved riddle tiles remaining on this level.</summary>
    public (int SecretDoors, int Riddles) RemainingSecrets() =>
        (Maze.HiddenSecretCount(), Maze.CountFeature(CellFeature.Riddle));

    /// <summary>How many barred gates still seal vaults on this level (a lever raises them).</summary>
    public int BarredGates() => Maze.GateCount();

    /// <summary>How many locked doors remain on this level (each needs a carried key to open).</summary>
    public int LockedDoors() => Maze.LockedDoorCount();

    /// <summary>True when the party stands on a pull-able rune lever.</summary>
    public bool OnLever => CurrentCell.Feature == CellFeature.Lever;

    /// <summary>
    /// Hauls the rune lever underfoot, raising every barred gate on the level so its sealed
    /// vaults can be reached. The lever locks spent afterward (the gates stay open).
    /// </summary>
    public LeverResult PullLever()
    {
        if (CurrentCell.Feature != CellFeature.Lever)
            return new LeverResult(0, "There is no lever here.");

        var opened = Maze.OpenAllGates();
        CurrentCell.Feature = CellFeature.None; // the lever locks into place, spent
        MarkVisited();
        var message = opened switch
        {
            0 => "You haul the lever down with a heavy clunk — but nothing stirs. Its work is already done.",
            1 => "You haul the lever down. Stone grinds as a barred gate rises somewhere on this level!",
            _ => $"You haul the lever down. Stone grinds as {opened} barred gates rise across this level!"
        };
        return new LeverResult(opened, message);
    }

    /// <summary>True when the party stands on an unsolved riddle tile.</summary>
    public bool OnRiddle => CurrentCell.Feature == CellFeature.Riddle;

    /// <summary>
    /// Answers the riddle underfoot. A correct answer grants gold and loot and the runes go dark;
    /// a wrong answer leaves the tile to try again.
    /// </summary>
    public RiddleResult AnswerRiddle(string answer)
    {
        var log = new List<string>();
        if (CurrentCell.Feature != CellFeature.Riddle)
            return new RiddleResult(false, new[] { "There is no riddle here." });

        if (!Riddles.Get(CurrentCell.RiddleId).Accepts(answer))
        {
            log.Add("The runes flare red and stay dark — that is not the answer.");
            return new RiddleResult(false, log);
        }

        var (gold, items) = Loot.RiddleReward(_rng, Depth);
        Party.Gold += gold;
        log.Add("The runes glow gold — you have answered true!");
        log.Add($"A hidden cache yields {gold} gold.");
        foreach (var item in items)
        {
            Party.Inventory.Add(item);
            log.Add($"Found: {item.DisplayName}.");
        }
        CurrentCell.Feature = CellFeature.None; // solved
        return new RiddleResult(true, log);
    }

    /// <summary>True when the party stands on an unopened treasure chest (plain or ornate).</summary>
    public bool OnChest => CurrentCell.Feature is CellFeature.Chest or CellFeature.OrnateChest;

    private static readonly Element[] ChestTrapElements =
        { Element.Fire, Element.Cold, Element.Lightning, Element.Poison };

    /// <summary>The odds a plain chest is really a disguised mimic — rising slowly with depth.</summary>
    private double MimicChance => Math.Min(0.25, 0.08 + 0.01 * Depth);

    /// <summary>
    /// The chance that making camp is interrupted by a wandering ambush — riskier the deeper
    /// you rest, but a watchful Rogue and a Bard's soothing song each make the camp safer.
    /// </summary>
    public double CampAmbushChance
    {
        get
        {
            var chance = 0.15 + 0.02 * Depth;
            if (Party.Members.Any(m => !m.IsDead && m.Class == CharacterClass.Rogue)) chance -= 0.10;
            if (Party.Members.Any(m => !m.IsDead && m.CanSing)) chance -= 0.08;
            return Math.Clamp(chance * _difficulty.CampRisk, 0.05, 0.5);
        }
    }

    /// <summary>
    /// Makes camp to recover. There's a depth-scaled chance wandering monsters ambush the
    /// resting party (interrupting the rest with a fight); otherwise every living hero recovers
    /// half their maximum hit points and spell points.
    /// </summary>
    public CampResult Camp()
    {
        var log = new List<string>();
        if (_rng.Chance(CampAmbushChance))
        {
            _stepsSinceEncounter = 0;
            log.Add("You bed down to rest — but wandering monsters fall upon the camp!");
            return new CampResult(false, log, _encounters.CreateRandom(Depth));
        }

        foreach (var m in Party.Members.Where(m => !m.IsDead))
        {
            m.HitPoints = Math.Min(m.EffectiveMaxHitPoints, m.HitPoints + Math.Max(1, m.EffectiveMaxHitPoints / 2));
            m.SpellPoints = Math.Min(m.EffectiveMaxSpellPoints, m.SpellPoints + Math.Max(1, m.EffectiveMaxSpellPoints / 2));
            m.RefreshBardTunes(); // a Bard recovers their repertoire of tunes by resting
        }
        _stepsSinceEncounter = 0;

        var watcher = Party.Members.FirstOrDefault(m => !m.IsDead && m.Class == CharacterClass.Rogue);
        if (watcher is not null)
            log.Add($"{watcher.Name} keeps watch as the party makes camp.");
        log.Add("The party rests undisturbed — hit points and spell points recovered.");
        return new CampResult(true, log, null);
    }

    /// <summary>
    /// Opens the chest underfoot. A plain chest may instead prove to be a mimic and lunge
    /// (returning an encounter to fight). Otherwise the ablest Rogue tries to disarm any trap;
    /// failure springs an elemental snare on a random hero (warding gear softens it). Ornate
    /// chests are always trapped, harder to crack, and richer. Either way the chest is emptied.
    /// </summary>
    public ChestResult OpenChest()
    {
        var log = new List<string>();
        var ornate = CurrentCell.Feature == CellFeature.OrnateChest;
        if (CurrentCell.Feature != CellFeature.Chest && !ornate)
            return new ChestResult(new[] { "There is no chest here." }, Array.Empty<Item>(), false);

        // A plain chest may be a mimic in disguise; a gilded one is always genuine treasure.
        if (!ornate && _rng.Chance(MimicChance))
        {
            CurrentCell.Feature = CellFeature.None; // the "chest" lunges — nothing left to loot
            log.Add("As you reach for the latch, the lid splits into a maw of teeth — it's a Mimic!");
            return new ChestResult(log, Array.Empty<Item>(), false, ChestMimic.EncounterFor(Depth));
        }

        var trapSprang = false;
        if (_rng.Chance(ornate ? 1.0 : 0.55))
        {
            var rogue = Party.Members
                .Where(m => !m.IsDead && m.Class == CharacterClass.Rogue)
                .OrderByDescending(m => m.Level)
                .FirstOrDefault();
            // Deft Rogue hands disarm most snares; ornate locks are tougher, and a party
            // without any Rogue usually springs the trap.
            var baseChance = ornate ? 0.30 : 0.45;
            var disarmChance = rogue is null
                ? (ornate ? 0.05 : 0.15)
                : Math.Min(0.95, baseChance + 0.04 * rogue.Level + 0.03 * rogue.DexterityBonus);
            if (rogue is not null && _rng.Chance(disarmChance))
                log.Add($"{rogue.Name} deftly disarms the {(ornate ? "ornate " : "")}chest's trap.");
            else
            {
                trapSprang = true;
                SpringChestTrap(log, ornate);
            }
        }
        else
        {
            log.Add("The chest is unlatched without a hitch.");
        }

        var (gold, items) = Loot.RollChest(_rng, Depth, ornate);
        Party.Gold += gold;
        log.Add($"You loot {gold} gold from the {(ornate ? "ornate " : "")}chest.");
        foreach (var item in items)
        {
            Party.Inventory.Add(item);
            log.Add($"Found: {item.DisplayName}.");
        }
        foreach (var set in AccessorySets.SetsIn(items))
            log.Add($"✦ A matched set — the {set.Name}! {set.Description}");

        CurrentCell.Feature = CellFeature.None; // the chest is now empty
        return new ChestResult(log, items, trapSprang);
    }

    /// <summary>An elemental snare bites a random hero; warding gear halves a matching blast.</summary>
    private void SpringChestTrap(List<string> log, bool ornate)
    {
        var living = Party.Members.Where(m => !m.IsDead).ToList();
        if (living.Count == 0) return;
        var victim = living[_rng.Next(0, living.Count)];
        var element = ChestTrapElements[_rng.Next(0, ChestTrapElements.Length)];
        var dmg = ornate ? _rng.Roll(3, 6, Depth * 2) : _rng.Roll(2, 6, Depth);
        var warded = victim.Resists(element);
        if (warded) dmg = Math.Max(1, dmg / 2);
        victim.ApplyDamage(dmg);
        var elementName = element.ToString().ToLowerInvariant();
        log.Add($"A {elementName} trap springs! {victim.Name} takes {dmg} damage"
            + (warded ? $" (warded)" : "") + ".");
        if (element == Element.Poison && !victim.IsDead && _rng.Chance(0.5))
        {
            victim.Inflict(StatusEffect.Poisoned);
            log.Add($"{victim.Name} is poisoned!");
        }
        if (victim.IsDead) log.Add($"{victim.Name} has fallen!");
    }

    private MoveResult SpringTrap()
    {
        var living = Party.Members.Where(m => !m.IsDead).ToList();
        if (living.Count == 0)
            return new MoveResult(MoveResultKind.Trapped, "A trap springs in the empty hall.");

        var victim = living[_rng.Next(0, living.Count)];
        var dmg = _rng.Roll(1, 6, 2);
        victim.ApplyDamage(dmg);
        var msg = $"A trap springs! {victim.Name} takes {dmg} damage.";
        if (victim.IsDead) msg += $" {victim.Name} has fallen!";
        return new MoveResult(MoveResultKind.Trapped, msg);
    }

    private bool CheckForEncounter(out Encounter? encounter)
    {
        encounter = null;
        _stepsSinceEncounter++;
        // Grace period after a fight, then a rising chance to be ambushed.
        if (_stepsSinceEncounter < 2) return false;
        var chance = (0.10 + 0.03 * (_stepsSinceEncounter - 2)) * _difficulty.EncounterChance;
        if (!_rng.Chance(Math.Min(chance, 0.45))) return false;

        _stepsSinceEncounter = 0;
        encounter = _encounters.CreateRandom(Depth);
        return true;
    }

    /// <summary>
    /// The depth's experience multiplier. Level-up cost doubles each level
    /// (1000 · 2^(level-1)), so XP rewards grow geometrically with depth — about +25%
    /// per floor — to keep the party's level climbing in step with the 20-floor descent.
    /// </summary>
    public double DepthXpScale => Math.Pow(1.25, Math.Min(Depth, Bosses.FinalDepth) - 1);

    /// <summary>
    /// Award experience and gold to the survivors after a won fight. Level-ups are
    /// not applied here — the party must visit the Review Board back in town.
    /// </summary>
    public IReadOnlyList<string> ApplyVictory(Encounter encounter)
    {
        var log = new List<string>();
        var living = Party.Members.Where(m => !m.IsDead).ToList();
        if (living.Count == 0) return log;

        // Both wandering fights and boss lairs flow through here, so scaling once covers both.
        var scaledXp = (long)(encounter.TotalExperience * DepthXpScale * _difficulty.Reward);
        var xpEach = scaledXp / living.Count;
        Party.Gold += (int)Math.Round(encounter.TotalGold * _difficulty.Reward);

        foreach (var member in living)
        {
            member.Experience += xpEach;
            if (Progression.CanLevelUp(member))
                log.Add($"{member.Name} has learned enough to advance — visit the Review Board.");
        }

        var loot = Loot.Roll(encounter, _rng, Depth);
        foreach (var item in loot)
        {
            Party.Inventory.Add(item);
            log.Add($"Found: {item.DisplayName}.");
        }
        foreach (var set in AccessorySets.SetsIn(loot))
            log.Add($"✦ You've recovered the {set.Name} set — {set.Description}");
        return log;
    }

    /// <summary>Places the party back at this level's entrance, e.g. on re-entry from town.</summary>
    public void ReturnToEntrance()
    {
        Party.Position = Maze.StartPosition;
        Party.Facing = Maze.StartFacing;
        _stepsSinceEncounter = 0;
        MarkVisited();
    }

    public MoveResult Descend()
    {
        Depth++;
        // Revisit a level you've already mapped, or carve a fresh one the first time down.
        if (_levels.TryGetValue(Depth, out var existing))
        {
            Maze = existing;
        }
        else
        {
            Maze = new MazeBuilder(_rng).Build($"Catacombs — Level {Depth}", Maze.Width, Maze.Height);
            _levels[Depth] = Maze;
        }
        Party.Position = Maze.StartPosition;
        Party.Facing = Maze.StartFacing;
        _stepsSinceEncounter = 0;
        MarkVisited();
        return new MoveResult(MoveResultKind.Moved, $"You descend to level {Depth}.");
    }

    /// <summary>Climbs to the level above, arriving at its downward stair with its map intact.</summary>
    public MoveResult Ascend()
    {
        if (!CanAscend)
            return new MoveResult(MoveResultKind.Moved, "There is nowhere further up to climb.");
        Depth--;
        Maze = _levels[Depth]; // always present — you descended through it to get here
        Party.Position = Maze.PositionOf(CellFeature.StairsDown) ?? Maze.StartPosition;
        Party.Facing = Maze.StartFacing;
        _stepsSinceEncounter = 0;
        MarkVisited();
        return new MoveResult(MoveResultKind.Moved, $"You climb back up to level {Depth}.");
    }

    // Darkness blots out the map: you can't record where you can't see — unless you carry light.
    private void MarkVisited()
    {
        if (CurrentCell.Feature != CellFeature.Darkness || HasLight)
            CurrentCell.Visited = true;
    }

    private string DescribeView()
    {
        var ahead = Maze.CanMove(Party.Position, Party.Facing) ? "The passage continues ahead." : "A wall looms ahead.";
        return ahead;
    }
}
