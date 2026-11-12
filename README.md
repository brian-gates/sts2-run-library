# Run Library: Slay the Spire 2 mod

![Run Library](workshop/preview.png)

A small host-side companion mod for the free **STS2 Save Sync** app. When Save Sync loads a co-op run from
your save library through a Host link, the game opens straight into that run's **Multiplayer > Load**
lobby instead of stopping at the main menu, so your friends can join right away.

Only the host needs it; friends joining your lobby don't. It never goes online and never writes your saves.
Unofficial fan project, not affiliated with Mega Crit.

## Requirements

- **Slay the Spire 2** (built for v0.107.1; the manifest declares `min_game_version` 0.107.1).
- The free **STS2 Save Sync** companion app (Microsoft Store, or see
  [sts2.thegates.house](https://sts2.thegates.house)) and access to a save library in it. Without the app
  the mod does nothing.

## Install

- **Steam Workshop:** subscribe to **Run Library**, enable it in **Settings > Mods**, restart the game.
- **Manual:** quit the game, then put `STS2_RunLibrary.dll` and `STS2_RunLibrary.json` (the
  `mod_manifest.json`, renamed) in the game's `mods/` folder: `SlayTheSpire2.app/Contents/MacOS/mods/` on
  macOS, `<game dir>/mods/` on Windows/Linux. Never overwrite the DLL while the game is running.

## How it works

When you open a Host link in STS2 Save Sync, the app loads that multiplayer run into your save slot, leaves
a one-time note for the mod and launches the game. When the main menu appears, the mod reads the note,
deletes it (always, valid or not, so it acts at most once) and, if everything matches, opens
**Multiplayer > Load** using the game's own Load flow (same checks, same lobby screen). If anything does not
match, nothing happens and you stay on the main menu.

The note is `sts2savesync-autoload.json` in the game's user data folder (`~/Library/Application
Support/SlayTheSpire2` on macOS, `%APPDATA%\SlayTheSpire2` on Windows). It is used only if all of these hold:

- it is format version 1 and was written in the last 5 minutes (up to 1 minute in the future is tolerated
  for clock skew);
- its Steam ID is the Steam user running the game;
- its mode (`modded` or `regular`) matches the game (with any mod loaded the game uses its modded saves);
- its profile is the active profile (the mod never switches profiles);
- the active profile has a multiplayer run save, and that save is the same run (same run id as the save
  library: sha256 of start time, seed and sorted Steam IDs, first 16 hex characters).

Every decision is logged to the Godot log with a `[RunLibrary]` prefix.

## Privacy

- No network access of any kind. The only input is the local note the app writes.
- Never writes or changes saves. The only file it touches is the note, which it deletes after reading.
- `affects_gameplay: false`: it only opens a menu screen you could open yourself.

## Build and test

Requires the game installed (the build references its own assemblies; nothing proprietary is bundled) and
the **.NET 9 SDK** or newer.

```bash
# macOS
GAME_DIR="$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2"
dotnet build STS2_RunLibrary.csproj -c Release -o out -p:STS2GameDir="$GAME_DIR"
dotnet test tests/STS2_RunLibrary.Tests.csproj
```

## Steam Workshop

Publishing uses MegaCrit's official [`sts2-mod-uploader`](https://github.com/megacrit/sts2-mod-uploader),
which talks to Steam directly, so it runs **locally with the Steam client open and logged in**, never in CI.
Workshop sources live in `workshop/` (`workshop.json`, `preview.png`); the script builds Release and stages
`dist/workshop/` (git-ignored) with `content/` holding exactly what subscribers receive.

```bash
./scripts/package-workshop.sh                                     # build and stage only
MODUPLOADER=/path/to/ModUploader ./scripts/package-workshop.sh --upload
```

The first upload creates the Workshop item and writes `dist/workshop/mod_id.txt`; the script copies it to
`workshop/mod_id.txt`. Commit that file so later uploads update the same item.

## Credits

- [Multiplayer Save Slots](https://github.com/jadistanbelly/sts2-multiplayer-save-slots) by jadistanbelly
  (Unlicense, public domain) was the prior art for the host Load flow: its hook points and fail-closed
  approach informed this mod. No code is copied and there is no runtime dependency.
- The run id logic mirrors the STS2 Save Sync save library so both agree on which run is which.
- The preview image uses [Kreon](https://fonts.google.com/specimen/Kreon) (SIL Open Font License).

## License

MIT, see [LICENSE](LICENSE). Slay the Spire 2 is (c) Mega Crit; this mod bundles none of its files.
