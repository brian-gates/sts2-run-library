# Run Library: Slay the Spire 2 mod

A host-side companion mod for the **STS2 Save Sync** app. It never goes online; it only reads small local
files the app leaves on the same computer.

## Features

- **Auto-load into the lobby.** When you open a play link in STS2 Save Sync, the app loads that multiplayer
  run into your save slot, leaves a one-time note for the mod and launches the game. When the main menu
  appears, the mod checks the note and, if everything matches, opens **Multiplayer > Load** for you, using
  the game's own Load flow (same checks, same lobby screen). If anything does not match, nothing happens and
  you stay on the main menu.

The mod never writes or changes your saves. The note is deleted as soon as it is read, so it acts at most
once.

## When auto-load acts

The note (`sts2savesync-autoload.json` in the game's user data folder: `~/Library/Application
Support/SlayTheSpire2` on macOS, `%APPDATA%\SlayTheSpire2` on Windows) is used only if all of these hold:

- it is format version 1 and was written in the last 5 minutes (up to 1 minute in the future is tolerated
  for clock skew);
- its Steam ID is the Steam user running the game;
- its mode (`modded` or `regular`) matches the game (with any mod loaded the game uses its modded saves);
- its profile is the active profile (the mod never switches profiles);
- the active profile has a multiplayer run save, and that save is the same run (same run id as the app:
  sha256 of start time, seed and sorted Steam IDs, first 16 hex characters).

Every decision is logged to the Godot log with a `[RunLibrary]` prefix.

## Requirements

- **Slay the Spire 2** installed (the build references the game's own assemblies; nothing proprietary is
  bundled).
- The **.NET 9 SDK** (or newer) to build.

## Build

```bash
# macOS
GAME_DIR="$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2"
dotnet build STS2_RunLibrary.csproj -c Release -o out -p:STS2GameDir="$GAME_DIR"
dotnet test tests/STS2_RunLibrary.Tests.csproj
```

## Install

Mods load at startup, so quit the game first, then copy the DLL and manifest into the game's `mods` folder:

- macOS: `$GAME_DIR/SlayTheSpire2.app/Contents/MacOS/mods/`
- Windows/Linux: `$GAME_DIR/mods/`

```bash
cp out/STS2_RunLibrary.dll "$GAME_DIR/SlayTheSpire2.app/Contents/MacOS/mods/"
cp mod_manifest.json "$GAME_DIR/SlayTheSpire2.app/Contents/MacOS/mods/STS2_RunLibrary.json"
```

Never overwrite the DLL while the game is running.

## License

MIT. See [LICENSE](LICENSE).
