#!/usr/bin/env bash
# Build Run Library (Release) and stage the Steam Workshop workspace in dist/workshop/:
#   workshop.json  (from workshop/workshop.json)
#   image.png      (preview, from workshop/preview.png)
#   content/       (STS2_RunLibrary.dll + STS2_RunLibrary.json, exactly what subscribers get)
#   mod_id.txt     (copied from workshop/mod_id.txt once the item exists, so uploads update it)
# It does NOT upload unless you pass --upload. Uploading uses MegaCrit's sts2-mod-uploader and talks to
# Steam directly, so it must run locally with the Steam client open and logged in (never in CI).
#
# Env: STS2GameDir (game install; default: the macOS Steam path)
#      MODUPLOADER (uploader CLI; default ./tools/ModUploader, may be a symlink)
set -euo pipefail
cd "$(dirname "$0")/.."

UPLOAD=0
[[ "${1:-}" == "--upload" ]] && UPLOAD=1

GAME_DIR="${STS2GameDir:-$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2}"
WS="dist/workshop"

echo "==> Building Release (STS2GameDir=$GAME_DIR)"
dotnet build STS2_RunLibrary.csproj -c Release -o out -p:STS2GameDir="$GAME_DIR"

echo "==> Staging $WS"
rm -rf "$WS"
mkdir -p "$WS/content"
cp out/STS2_RunLibrary.dll "$WS/content/STS2_RunLibrary.dll"
cp mod_manifest.json "$WS/content/STS2_RunLibrary.json"
cp workshop/workshop.json "$WS/workshop.json"
cp workshop/preview.png "$WS/image.png"
size=$(wc -c < "$WS/image.png" | tr -d ' ')
(( size < 1048576 )) || { echo "image.png is $size bytes; Steam wants < 1 MB" >&2; exit 1; }
[[ -f workshop/mod_id.txt ]] && cp workshop/mod_id.txt "$WS/mod_id.txt"

echo "    $(cd "$WS" && find . -type f | sort | tr '\n' ' ')"

UPLOADER="${MODUPLOADER:-./tools/ModUploader}"
ABS_WS="$(cd "$WS" && pwd)"
if (( UPLOAD )); then
  [[ -x "$UPLOADER" ]] || { echo "ModUploader not found at $UPLOADER (set MODUPLOADER)" >&2; exit 1; }
  # Steamworks reads steam_appid.txt from the working directory and loads libsteam_api next to the binary,
  # so run from the real (symlink-resolved) uploader directory.
  UPLOADER_REAL="$(readlink -f "$UPLOADER")"
  UPLOADER_DIR="$(dirname "$UPLOADER_REAL")"
  echo "==> Uploading via $UPLOADER_REAL (Steam must be running and logged in)"
  ( cd "$UPLOADER_DIR" && "./$(basename "$UPLOADER_REAL")" upload -w "$ABS_WS" )
  if [[ -f "$WS/mod_id.txt" ]]; then
    cp "$WS/mod_id.txt" workshop/mod_id.txt
    echo "==> Workshop item id: $(cat workshop/mod_id.txt) (saved to workshop/mod_id.txt; commit it)"
  fi
else
  cat <<EOM

==> Staged, not uploaded. To publish (Steam client open and logged in):
      MODUPLOADER=/path/to/ModUploader ./scripts/package-workshop.sh --upload
    which runs: (cd <uploader dir> && ./ModUploader upload -w "$ABS_WS")
    First upload creates a NEW Workshop item and writes $WS/mod_id.txt; the script copies it to
    workshop/mod_id.txt. Commit that file so later uploads update the same item.
EOM
fi
