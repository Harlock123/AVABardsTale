# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing the game, its controls, the
per-platform setup and what is implemented; this file covers how the code is put together and the rules that keep
it working.

## What this is

A fan remake of Interplay's 1985 dungeon crawler The Bard's Tale, in C# on .NET 9 and Avalonia 11 (MVVM with
CommunityToolkit.Mvvm). One shared UI library runs under thin platform heads: desktop (Windows, macOS, Linux),
Android, iOS and the browser (WebAssembly). Sound effects and music are synthesised in code.

## Commands

```bash
dotnet build src/BardsTale.Desktop/BardsTale.Desktop.csproj    # desktop head; compiles Core and UI too
dotnet test tests/BardsTale.Tests/BardsTale.Tests.csproj        # xUnit, no window opens
dotnet run --project src/BardsTale.Desktop                      # play it
scripts/publish.sh [rid ...]                                    # self-contained single-file builds into dist/
scripts/screenshots.sh                                          # regenerate SCREENSHOTS/ (opens the real UI)
```

- All projects target `net9.0` (or `net9.0-android`, `-ios`, `-browser`). There is no `global.json`.
- `dotnet build BardsTale.slnx` also builds the Android and iOS heads, so it needs the `android` and `ios`
  workloads (and Xcode). The Browser head is not in the solution; it needs the `wasm-tools` workload.
- The version lives in `Directory.Build.props` (`<Version>`); `VERSION=1.2.0 scripts/publish.sh` overrides it.
- Environment variables:
  - `BT_SHOT=1` and `BT_SHOT_DIR` put the desktop app into screenshot-capture mode (silent, drives itself).
  - `BT_AUDIO_LOG` logs the Windows audio backend.
  - `BT_DUMPMUSIC=1` makes `MusicTests` dump the generated music.

## Layout

- `src/BardsTale.Core/`: the game rules, with no UI dependency.
  - `Characters/`, `Items/`, `Magic/`, `Combat/` (`CombatEngine` resolves rounds; `MonsterCatalog`, `Bosses`).
  - `Dungeon/` (`MazeBuilder`, `Maze`, events, riddles), `Town/` (`TownMapBuilder`), `Quests/`, `Lore/`.
  - `Game/GameSession.cs` is the whole save: party, town, dungeons and the shared random source.
  - `Persistence/GameSerializer.cs` turns a session into JSON and back.
  - `Util/IRandomSource.cs`: the random-number abstraction and its dice helpers.
- `src/BardsTale.UI/`: the shared Avalonia app: `App`, `Views/`, `ViewModels/`, custom render controls in
  `Controls/` (`DungeonView`, `MiniMap`), `MovementKeys.cs` (key handling), `Settings/` and `Services/` (saves,
  run history).
  - `Audio/`: per-platform sound and music backends behind `IAudioService` and `IMusicService`: WinMM on
    Windows, an external player on Linux, `afplay` on macOS. `ToneSynth` and `MusicSynth` generate the sounds.
- `src/BardsTale.Desktop/`, `BardsTale.Android/`, `BardsTale.iOS/`, `BardsTale.Browser/`: thin heads, each just an
  entry point plus its own audio (and, for the browser, an IndexedDB save store and PWA files in `wwwroot/`).
- `tests/BardsTale.Tests/`: xUnit 2 tests over Core and the view models. `MemorySaveStore` is an in-memory save
  store for tests.

## Rules that keep it working

- **Core stays UI-free.** Game rules go in `BardsTale.Core`, where tests reach them directly. `BardsTale.UI`
  presents them; the heads only start the app and supply platform services.
- **Seeded randomness.** Draw random numbers from the session's `IRandomSource` (`session.Rng`, a
  `SystemRandomSource` that takes an optional seed), so tests and the daily challenge are repeatable. Don't create
  a new `Random` in game code.
- **Platform services go through factories.** `App.SaveStoreFactory`, `App.AudioFactory` and `App.MusicFactory`
  pick the save store and audio backends; a head or test replaces them rather than branching inside the UI.
- **Saves.** The desktop `SaveService` writes JSON slots to the app-data folder (`SpecialFolder.ApplicationData`,
  folder `BardsTale`). Settings and run history go through the same `ISaveStore`. Tests pass a temporary
  folder (`Path.GetTempPath()`) or `MemorySaveStore`, never the player's folder.
- **Old saves must still load.** Settings use nullable fields so files written by older versions get defaults;
  keep that pattern when adding settings or save data.
- **Tests run one at a time.** `TestParallelization.cs` turns off parallel runs because some view-model tests
  change the shared `AppSettings.Current`.

## Making a change

- Add or update tests with every rules change, and keep `dotnet test` passing.
- If the change shows on screen, regenerate the screenshots with `scripts/screenshots.sh` (needs a desktop
  session) and look at them.
- Update `README.md` (features, controls, the solution table) to match.

## CI and releases

`.github/workflows/build.yml` builds the desktop head and runs the tests on every push and pull request, on .NET
9. It then publishes self-contained single-file builds for win-x64/x86/arm64, linux-x64/arm64/arm and
osx-x64/arm64, renamed `AVABardsTale`, and keeps them as workflow artifacts. Pushes to `main` replace the assets of
the `latest` pre-release, and a `v*` tag makes a proper release. The Android, iOS and Browser heads are not built
in CI.
