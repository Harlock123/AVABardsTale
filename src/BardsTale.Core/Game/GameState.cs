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
    Chest
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

/// <summary>
/// The live game world: the party exploring the current maze level. Owns movement,
/// wandering-monster checks and post-combat rewards.
/// </summary>
public sealed class GameState
{
    private readonly IRandomSource _rng;
    private readonly EncounterFactory _encounters;
    private int _stepsSinceEncounter;

    // Each depth keeps its own maze, so a level's layout and explored map persist
    // when you climb away and return. Keyed by depth.
    private readonly Dictionary<int, Maze> _levels = new();

    public GameState(Party party, Maze maze, IRandomSource rng)
    {
        Party = party;
        Maze = maze;
        _rng = rng;
        _encounters = new EncounterFactory(rng);
        _levels[Depth] = maze;
        Party.Position = maze.StartPosition;
        Party.Facing = maze.StartFacing;
        MarkVisited();
    }

    /// <summary>Restores a dungeon from a saved game, preserving depth, position and the explored map.</summary>
    public GameState(Party party, Maze maze, IRandomSource rng, int depth, Position position, Direction facing,
        int lightRemaining = 0)
    {
        Party = party;
        Maze = maze;
        _rng = rng;
        _encounters = new EncounterFactory(rng);
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
            return new MoveResult(MoveResultKind.BlockedByWall, "A wall blocks your way.");

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
                    Bosses.Create(Depth));
            case CellFeature.Chest:
                return new MoveResult(MoveResultKind.Chest,
                    "A heavy treasure chest sits here, its lid latched shut.");
            case CellFeature.OrnateChest:
                return new MoveResult(MoveResultKind.Chest,
                    "An ornate, gold-filigreed chest rests here — clearly valuable, and surely trapped.");
        }

        if (CheckForEncounter(out var encounter))
            return new MoveResult(MoveResultKind.Encounter, "Monsters block your path!", encounter);

        return new MoveResult(MoveResultKind.Moved, DescribeView());
    }

    /// <summary>Marks the current boss lair as cleared so its fight does not recur.</summary>
    public void ClearBoss()
    {
        if (CurrentCell.Feature == CellFeature.BossLair)
            CurrentCell.Feature = CellFeature.None;
    }

    /// <summary>True when the party stands on an unopened treasure chest (plain or ornate).</summary>
    public bool OnChest => CurrentCell.Feature is CellFeature.Chest or CellFeature.OrnateChest;

    private static readonly Element[] ChestTrapElements =
        { Element.Fire, Element.Cold, Element.Lightning, Element.Poison };

    /// <summary>The odds a plain chest is really a disguised mimic — rising slowly with depth.</summary>
    private double MimicChance => Math.Min(0.25, 0.08 + 0.01 * Depth);

    /// <summary>The chance that making camp is interrupted by a wandering ambush — riskier the deeper you rest.</summary>
    private double CampAmbushChance => Math.Min(0.5, 0.15 + 0.02 * Depth);

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
            m.HitPoints = Math.Min(m.MaxHitPoints, m.HitPoints + Math.Max(1, m.MaxHitPoints / 2));
            m.SpellPoints = Math.Min(m.MaxSpellPoints, m.SpellPoints + Math.Max(1, m.MaxSpellPoints / 2));
        }
        _stepsSinceEncounter = 0;
        log.Add("The party makes camp and rests undisturbed — hit points and spell points recovered.");
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
        var chance = 0.10 + 0.03 * (_stepsSinceEncounter - 2);
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
        var scaledXp = (long)(encounter.TotalExperience * DepthXpScale);
        var xpEach = scaledXp / living.Count;
        Party.Gold += encounter.TotalGold;

        foreach (var member in living)
        {
            member.Experience += xpEach;
            if (Progression.CanLevelUp(member))
                log.Add($"{member.Name} has learned enough to advance — visit the Review Board.");
        }

        foreach (var item in Loot.Roll(encounter, _rng, Depth))
        {
            Party.Inventory.Add(item);
            log.Add($"Found: {item.DisplayName}.");
        }
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
