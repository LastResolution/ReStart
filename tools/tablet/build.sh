#!/usr/bin/env bash
# Cross-builds the tablet edition for win-x64 from Linux into artifacts/tablet/.
# Folder publish (not single-file) + ReadyToRun, so the app starts without
# extracting itself or JIT-compiling everything.
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
out="${1:-$root/artifacts/tablet}"
common=(-c Release -r win-x64 --self-contained true -p:EnableWindowsTargeting=true -p:DebugType=None)
rm -rf "$out"
dotnet publish "$root/src/ReStart.TileHost/ReStart.TileHost.csproj" "${common[@]}" \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$out/TileHost" -v quiet -nologo
dotnet publish "$root/src/ReStart.App/ReStart.App.csproj" "${common[@]}" \
  -p:SkipTileHostCopy=true -p:PublishReadyToRun=true -o "$out" -v quiet -nologo
cp "$root/LICENSE" "$out/"
echo "built $(find "$out" -type f | wc -l) files, $(du -sh "$out" | cut -f1) -> $out"
