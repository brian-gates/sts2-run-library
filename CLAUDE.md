# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**Run Library**, a host-side **Slay the Spire 2** (Godot/C#) mod that is the in-game half of the STS2 Save
Sync app (`~/code/sts2-save-sync`, server `~/code/sts2-saves`). Design spec:
`~/code/sts2-saves/docs/specs/2026-11-11-library-host-picker-design.md`. Hard rules from it: the mod never
goes online (it only reads local files the app writes), and hosts never rotate.

Current feature: **auto-load into the lobby** (`AutoLoad.cs`). The app writes
`<OS.GetUserDataDir()>/sts2savesync-autoload.json` after a play-link load and before launching the game. On
the main menu the mod reads it, deletes it (always, valid or not), validates it and, if it matches the active
multiplayer save, opens Multiplayer > Load.

## Build & test

```bash
GAME_DIR="$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2"
dotnet build STS2_RunLibrary.csproj -c Release -o out -p:STS2GameDir="$GAME_DIR"   # must be 0 warnings
dotnet test tests/STS2_RunLibrary.Tests.csproj
```

The `.csproj` derives the platform `data_*` dir holding `sts2.dll`, `GodotSharp.dll`, `0Harmony.dll`.
Tests compile the pure sources (`RunId.cs`, `AutoloadMarker.cs`) directly and need no game DLLs. Everything
touching the game is verified manually in-game; the owner installs the mod (do not copy into `mods/`, do not
launch the game). Never overwrite the DLL in place while the game runs.

## Architecture

- `RunLibraryMod.cs`: `[ModInitializer("Initialize")]`, `Log()` (`GD.Print("[RunLibrary] ...")`), and a
  Harmony postfix on `NMainMenu._Ready`.
- `AutoLoad.cs`: the game-facing flow. Mirrors the game's own `-fastmp load` path in
  `NMainMenu.CheckCommandLineArgs`: `SaveManager.LoadAndCanonicalizeMultiplayerRunSave(localSteamId)`, then
  `menu.OpenMultiplayerSubmenu().StartHost(run)` (what `NMultiplayerSubmenu.StartLoad`, the Load button, does
  on success), which pushes `NMultiplayerLoadGameScreen.InitializeAsHost` (a `LoadRunLobby`).
- `AutoloadMarker.cs` (pure): marker parsing (`AutoloadMarker.Parse`) and the marker-only rules
  (`AutoloadRules.Reject`: freshness, Steam ID, mode, profile).
- `RunId.cs` (pure): run id identical to `sts2-saves/src/save.ts computeRunId`. Change both together; the
  tests pin values produced by the TS implementation.

## Conventions

- Hooks must never break the menu: every patch body is wrapped in `try/catch` and logs.
- Log every decision with the `[RunLibrary]` prefix.
- Reuse the game's own methods for UI flows so its normal checks and screens run.
- No em dashes in user-facing text. Commit messages end with
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Steam Workshop

`scripts/package-workshop.sh` builds Release and stages `dist/workshop/` (git-ignored) from `workshop/`
(`workshop.json`, `preview.png`, and `mod_id.txt` once the item exists). `--upload` runs MegaCrit's
ModUploader (`$MODUPLOADER`, e.g. `~/code/sts2-damage-charts/tools/ModUploader`); the owner runs uploads.
Bump `version` in `mod_manifest.json` and `changeNote` in `workshop/workshop.json` together.
