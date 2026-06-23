# The Bard's Tale — Avalonia Remake

A cross-platform .NET 9 re-implementation of the classic 1985 dungeon crawler
*The Bard's Tale*, built with [Avalonia](https://avaloniaui.net/) and a clean
MVVM architecture.

It is a **complete playable loop, start to victory**: assemble and outfit a party
in the town of Skara Brae, descend through a multi-level procedurally generated
catacomb in a first-person grid view (with an auto-map and per-character turn-based
combat), survive status ailments, enemy spellcasters and drain attacks, plunder
magic loot, clear a named boss on each level, and finally destroy **Mangar the
Mad** to free the town and roll the credits. Town services — healing, levelling,
shopping, identification and resting — and a full save/load system round out the
loop.

## Solution layout

| Project | Purpose |
| --- | --- |
| `src/BardsTale.Core` | Pure C# game engine — no UI dependency. Characters, classes, races, items, spells, maze generation, movement, and the combat resolver. Fully unit-testable. |
| `src/BardsTale.UI` | Shared Avalonia MVVM presentation layer — `App`, all views & view models, the custom `DungeonView` / `MiniMap` render controls, the save service, and a single `MainView` root. **Platform-agnostic** (no backend package), so every head below reuses it unchanged. |
| `src/BardsTale.Desktop` | Thin desktop head (Windows / macOS / Linux) — just the entry point + `Avalonia.Desktop`, wrapping `MainView` in a `Window`. |
| `src/BardsTale.Browser` | Thin WebAssembly head — runs the same UI in the browser via `Avalonia.Browser` and the single-view lifetime. (Kept out of the default solution; see below.) |
| `tests/BardsTale.Tests` | xUnit suite (**146 tests**) covering geometry, maze generation & connectivity, special tiles, character creation, the full combat resolver (status effects, enemy spells, drain, summoning, surprise rounds, bosses), town services, save/load round-trips, and per-depth map persistence. |

The split follows Avalonia's standard cross-platform layout: a shared UI library plus
one thin "head" project per platform. Adding **Android** or **iOS** heads is the same
pattern — a small entry-point project referencing `BardsTale.UI`.

## Running

```bash
# from the repository root — desktop (Windows / macOS / Linux)
dotnet run --project src/BardsTale.Desktop

# run the tests
dotnet test
```

### Running in the browser (WebAssembly)

The `BardsTale.Browser` head runs the exact same UI in a browser via WebAssembly.
It needs the one-time .NET WASM workload, and is deliberately **excluded from
`BardsTale.slnx`** so the solution still builds/tests without that workload:

```bash
# one-time: install the WebAssembly build tools (needs elevated privileges)
sudo dotnet workload install wasm-tools-net9     # or: dotnet workload restore

# build & serve the browser app (opens a local dev server)
dotnet run --project src/BardsTale.Browser
```

> If your installed wasm workload targets .NET 10 instead, change the browser
> project's `TargetFramework` to `net10.0-browser`.

#### Persistent saves & installable PWA

The browser head is a **Progressive Web App**:

- **IndexedDB-backed saves.** Save slots persist across reloads and sessions via
  IndexedDB (`wwwroot/saveStore.js` ↔ `IndexedDbSaveStore`). Saves are **per-browser
  and per-origin** — they don't roam between browsers or devices. On startup the app
  requests *durable* storage (`navigator.storage.persist()`) to resist eviction
  (notably Safari's 7-day purge of script-writable storage).
- **Installable & offline.** A web manifest (`manifest.webmanifest`) makes the game
  installable to the desktop/home screen, and a service worker (`service-worker.js`)
  caches the app shell and WASM runtime so it runs offline after the first load.

Persistence is selected per-platform through `App.SaveStoreFactory`: the desktop heads
use the file-backed `SaveService`, the browser head swaps in `IndexedDbSaveStore`. Both
implement the shared async `ISaveStore` interface, so the view models are storage-agnostic.

### Controls

- **Arrow keys / WASD** — step forward & back, turn left & right (or use the on-screen buttons).
- **Descend** — when standing on a downward stairway, drop to a deeper, more dangerous level.
- **Ascend** — from an upward stairway below the entrance, climb back to the level
  above. Each level keeps its own layout and explored map, so revisiting one finds
  it exactly as you left it.
- **Exit to Town** — from the entrance stairway, return to Skara Brae.
- **Light** — conjure light (a Bard's *Watchwood Melody* for free, or a mage's
  *Mage Flame* for spell points) to see and map darkness for a number of steps.
- **Save / Load** (top bar) — opens a slot picker with **three named save slots**
  plus an **Autosave**. Each slot shows a summary (party size, gold, location). The
  whole game — party, gold, inventory, town position, and the explored dungeon — is
  persisted to disk. The game **autosaves** every time you return to town from the
  dungeon.

### Combat

Combat is round-based and resolved per character. Each round you give an order to
every able party member in turn:

- **Attack** the selected enemy group (front rank only).
- **Cast** a specific spell from that caster's known list — damage, healing,
  revival, or party buffs. Spell points are spent. Single-ally spells (heal, cure,
  revive) open a **target picker** so you choose exactly which companion to affect;
  damage spells hit the selected enemy group; party buffs/AOE need no target.
- **Sing** a Bard song for a party-wide effect (no spell points).
- **Use** a consumable from the party stash — healing potions, mana draughts,
  antidotes and resurrection dust, each targeted at a chosen ally.
- **Defend** to become harder to hit.

Pick a **Target** group on the left for offensive actions. **Auto** fills the
remaining orders with sensible defaults and resolves the round; **Undo Last**
steps back; **Flee** attempts to escape. Initiative interleaves party and monster
actions, and song/protection buffs last the whole encounter.

A fight may open with a **surprise round** rolled from the party's luck: an
**ambush** lets the monsters strike before you can react, while catching foes
**unawares** gives the party a free opening round. Luckier parties surprise more
and are ambushed less.

Some monsters inflict status ailments on a hit:

- **Poison** (Giant Spider) bites for damage at the start of each combat round and
  with every step taken in the dungeon.
- **Sleep** (Will-o-Wisp) makes a member skip their turns; they may wake on their
  own, are jolted awake when struck, and always rouse when the fight ends.
- **Paralysis** (Ghoul) locks a member out of acting until they shake it off or are
  cured.

Others have nastier special attacks on a hit:

- **Level drain** (Wraith) saps a level, lowering the victim's max HP/SP. A drained
  hero can't advance at the Review Board until the **Temple restores their lost
  levels** for a fee.
- **Stat drain** (Crypt Crawler) withers a random attribute; the **Temple restores
  withered attributes** for a fee too.
- **Gold theft** (Cutpurse) snatches coin straight from the party purse.

Cure ailments with the Conjurer's **Purify** spell, by **Resting**, or at the
**Temple** in town. Afflicted members show a coloured tag (POISON / SLEEP / PARA)
in the roster.

Some monsters cast spells of their own instead of attacking:

- The **Coven Witch** casts *Slumber*, putting party members to sleep (a luckier
  hero is likelier to resist).
- The **Mad God Acolyte** casts *Mending Chant*, healing its most-wounded ally —
  watch an enemy group's HP climb back up in the target list.
- The **Necromancer** casts *Summon Dead*, calling fresh **Skeleton reinforcements**
  into the fight (a new group appears in the target list, up to a cap).
- The **Spark Imp** and **Flame Shade** blast the whole party with area damage
  (*Spark* / *Cinderblast*); a luckier hero takes only half.
- The **Hex Adept** hurls a focused *Soul Bolt* at a single hero for heavy damage.

### The Skara Brae overworld

The game opens in the frozen town square of **Skara Brae**, which you explore in
the same first-person view as the dungeon (arrow keys / WASD; an auto-map tracks
where you've been, with gold markers for buildings). Walk up to a building and
press **Enter** (or the on-screen *Enter Building* button) to go inside:

- **Adventurers Guild** — create heroes (name, race, class, re-rollable
  attributes) and recruit them into a party of up to six, or hit **Quick Party**
  for a ready-made band. Dismiss heroes here too.
- **Garth's Equipment Shoppe** — buy and equip weapons, armour and shields
  (swapping gear sells the old piece back for half its value; class restrictions
  apply), stock up on potions, **appraise unidentified magic loot**, and **equip
  the gear you've looted** from your stash onto any hero (the piece they were
  wearing returns to the stash).
- **Temple of Healing** — heal the whole party, resurrect the fallen, or restore
  levels and attributes sapped by drain attacks, all for gold.
- **Review Board** — spend banked experience to level heroes up.
- **The Scarlet Bard** & **Mad Mable's** — taverns where you buy a round of drinks
  to loosen tongues and hear rumours and hints about the dangers below.
- **Garrick's Inn** — rent a room for the night to fully restore the party's hit
  points **and spell points** for a flat per-head fee (the fallen still need the
  Temple).
- **The Catacomb Stair** — step onto it to descend into the dungeon (and return to
  town from the dungeon's entrance stairway any time).

The town also has a **Cast a Spell** menu (no building needed): pick a caster's
restorative spell — heal an ally, heal the party, cure ailments, or revive the
fallen — and a target, and cast it for spell points instead of paying the Temple.

## What's implemented

- Six classic classes wired up (Warrior, Paladin, Rogue, Bard, Hunter, Monk and
  the four spellcasting schools), seven races, and the five core attributes
  (ST / IQ / DX / CN / LK).
- 4d6-drop-lowest attribute rolls, race modifiers, derived HP/SP, starting gear.
- A full town loop: recruitment, shopping, healing/revival and levelling, with a
  persistent party and shared gold purse that survive trips into the dungeon.
- Recursive-backtracker maze generation with extra loops, stairs, message tiles,
  and special tiles — **spinners** (randomise your facing), **teleporters** (whisk
  you across the level), **darkness** zones (the view blacks out and can't be
  mapped — until you conjure **light**), **traps** (spring for damage), and
  **anti-magic** zones (spells and songs fizzle for both sides). All survive save/load.
- A **fixed boss lair** on each level (guarding the descent) — a tough named boss
  flanked by minions, cleared permanently once beaten; the boss changes by depth.
  Every boss is **guaranteed to drop a magic item**.
- A **final boss and win condition**: descend to the deepest level to face
  **Mangar the Mad**. He drops his **signature legendary, Mangar's Staff**; destroy
  him and his guard to free Skara Brae and win the game. A **victory / credits
  screen** then shows your heroes and a tally of the run (battles won, monsters
  slain, gold plundered, deepest level reached), with the option to start anew.
- First-person pseudo-3D rendering with a receding vanishing point and depth shading.
- Auto-map that reveals only visited cells, with a directional party marker.
- Per-character turn-based combat: initiative, multiple attacks per round,
  monster groups, fleeing, and XP/gold rewards.
- Per-school spell lists (Conjurer / Magician / Sorcerer / Wizard) that grow with
  caster level, Bard songs, and encounter-long party buffs from protective spells
  and songs.
- Level-gated spell/song learning at the Review Board.
- Item drops from defeated foes into a shared party stash, consumable potions
  usable on a chosen ally mid-combat, potions for sale at Garth's, and equipping
  looted weapons/armour/shields from the stash (with the old piece stowed back).
- **Magic item drops** — enchanted "+1/+2/+3" weapons and armour (rarer the
  higher the bonus) that improve damage, to-hit and armour class, shown in gold.
  Magic loot drops **unidentified** (a vague "Unidentified Weapon" in purple). At
  Garth's you can reveal it three ways before equipping it: **pay Garth** a flat
  fee, have a **Rogue** appraise it for free (skill-based, can fail and be retried),
  or cast a Magician's **Scrye Sight** spell for a reliable read.
- **Per-depth dungeon persistence**: every level you descend into keeps its own
  layout and revealed auto-map, so climbing back up (or re-descending) finds each
  floor exactly as you left it rather than a freshly generated maze.
- Save / load of the entire session to JSON save files — three named slots plus an
  autosave-on-return-to-town — covering party, gold, inventory, town position, and
  **all explored dungeon levels** with the current depth and each floor's revealed map.

## Roadmap toward a fuller remake

The core game loop is complete and winnable. Natural next steps toward a fuller
recreation of the original:

- **Hand-designed dungeons** — replace (or mix in) the original's authored multi-level
  maps and riddles alongside the procedural generator, with stairs that link specific
  levels rather than always entrance-to-entrance.
- **More special tiles** — one-way doors, portcullises, and scripted message/event tiles.
- **Persistent per-level state** — remember which wandering monsters and loot a floor
  has yielded so a cleared level stays cleared on revisit.
- **Deeper character system** — the full original spell lists per school, more Bard
  song effects, class change, and a wider monster bestiary.
- **Audio & polish** — music and sound effects, richer combat animation, and a
  controller/keyboard-remap pass.
- **Mobile/browser targets** — the engine is UI-agnostic, so Avalonia's mobile and
  WASM heads are a natural extension of the desktop app.
