#!/usr/bin/env bash
# Test helper for the tablet edition on a Windows tablet reachable over SSH (ROG_HOST, default `rog`;
# OpenSSH server with PowerShell as the default shell, key login as the tablet's interactive user).
# Every command holds /tmp/rog-tablet.lock, so parallel agents never deploy to or
# drive the laptop screen at the same time. Wrap a multi-step test in `session`
# to keep the laptop for the whole sequence.
#
#   rog.sh deploy                 copy changed files of artifacts/tablet to the laptop slot
#   rog.sh restart [args]         stop every ReStart, start this slot's build with args
#   rog.sh stop                   stop every ReStart (graceful, then kill)
#   rog.sh tablet on|off|auto     force tablet mode / laptop mode / follow the keyboard
#   rog.sh shot out.png           screenshot the laptop screen (2560x1600 physical)
#   rog.sh run '<powershell>'     run PowerShell on the laptop (session 0, elevated, no desktop)
#   rog.sh userrun exe [args]     start a program in the user's desktop session (non-elevated)
#   rog.sh cmd <command> [arg]    send a --tablet-command (home, switcher, control-center, dock, ...)
#   rog.sh log [n]                tail %LOCALAPPDATA%\ReStart\tablet.log
#   rog.sh session cmd [args...]  run cmd (e.g. bash -c '...') holding the laptop lock;
#                                 rog.sh calls inside it do not re-lock
#   rog.sh touch '<steps>'        inject touch/pen in the user's session (win/touch.cs) and wait;
#                                 physical px, steps separated by ';':
#       tap x y [ms]              swipe x1 y1 x2 y2 [ms=300] [holdms=0]
#       down x y | move x y [ms] | hold ms | up      one contact, step by step
#       wait ms                   pen <step>  (synthetic pen instead of a finger)
#       shot local.png            screenshot at that moment (mid-gesture works), copied back at the end
#     e.g. rog.sh touch 'swipe 1280 1598 1280 900 250'            (swipe up from the bottom edge)
#          rog.sh touch 'down 1280 1598; move 1280 1100 300; hold 500; shot mid.png; up'
#   rog.sh testapp [title] [#rrggbb] [--ask|--refuse]   disposable window (win/testapp.cs) to close,
#                                 split or switch to instead of the user's apps; --ask prompts to save
#   rog.sh testapp-kill           close every test window
#
# Slots: ROG_SLOT=<name> (default main) deploys to %LOCALAPPDATA%\Programs\ReStartTablet-<name>.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"; root="$(cd "$here/../.." && pwd)"
slot="${ROG_SLOT:-main}"
art="$root/artifacts/tablet"; state="$root/artifacts/.rog-$slot"; mkdir -p "$state"
host="${ROG_HOST:-rog}"
# The tablet user's %LOCALAPPDATA%, asked once per host.
appdata_cache="/tmp/rog-tablet-localappdata.$host"
[ -s "$appdata_cache" ] || ssh -o ConnectTimeout=10 "$host" 'Write-Output $env:LOCALAPPDATA' | tr -d '\r' > "$appdata_cache"
dest="$(cat "$appdata_cache")\\Programs\\ReStartTablet-$slot"
win='C:\setup\restart-tablet'
if [ -z "${ROG_SESSION_HELD:-}" ]; then exec 9>/tmp/rog-tablet.lock; flock 9; export ROG_SESSION_HELD=1; fi

# Commands go through a script file: nested quoting through ssh + PowerShell 5.1 breaks.
ps() { local f name; f=$(mktemp --suffix=.ps1); printf '%s\n' "$1" > "$f"
  name="cmd-$$-$RANDOM.ps1"; scp -q "$f" "$host:C:/setup/restart-tablet/$name"; rm -f "$f"
  ssh -o ConnectTimeout=10 "$host" "powershell -NoProfile -ExecutionPolicy Bypass -File $win\\$name; \$c=\$LASTEXITCODE; Remove-Item $win\\$name; exit \$c"; }
sync_helpers() {
  # Per-checkout marker, so parallel worktrees each push their own new or changed helpers.
  local mark files
  mark="/tmp/rog-tablet-helpers.$(printf '%s' "$root" | sha1sum | cut -c1-8).synced"
  if [ -f "$mark" ]; then files=$(find "$here/win" -type f -newer "$mark"); else files=$(ls "$here"/win/*.ps1 "$here"/win/*.cs); fi
  if [ -n "$files" ]; then
    ssh "$host" "New-Item -ItemType Directory -Force $win | Out-Null" >/dev/null
    # shellcheck disable=SC2086
    scp -q $files "$host:C:/setup/restart-tablet/"; touch "$mark"
  fi
}
# Builds win/<name>.cs into <name>.exe on the laptop once per change (.NET Framework's csc ships with Windows).
compile_tool() { local n="$1"
  ssh "$host" "if (-not (Test-Path $win\\$n.exe) -or (Get-Item $win\\$n.cs).LastWriteTime -gt (Get-Item $win\\$n.exe).LastWriteTime) { & C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe /nologo /target:winexe /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:$win\\$n.exe $win\\$n.cs | Out-Host }"; }
touch_run() { local id="$$-$RANDOM" steps n=0 line i r; local -a shots=()
  steps="$state/touch-$id.txt"; : > "$steps"
  compile_tool touch
  while IFS= read -r line; do
    line="$(printf '%s' "$line" | sed 's/^ *//;s/ *$//')"; [ -z "$line" ] && continue
    if [[ "$line" == "shot "* ]]; then n=$((n+1)); shots+=("${line#shot }"); line="shot $win\\tshot-$id-$n.png"; fi
    printf '%s\r\n' "$line" >> "$steps"
  done < <(printf '%s\n' "$*" | tr ';' '\n')
  scp -q "$steps" "$host:C:/setup/restart-tablet/touch-$id.txt"; rm -f "$steps"
  userrun "$win\\touch.exe" "$win\\touch-$id.txt $win\\touch-$id.done"
  for _ in $(seq 1 90); do ssh "$host" "Test-Path $win\\touch-$id.done" | grep -q True && break; sleep 0.5; done
  ssh "$host" "Get-Content $win\\touch-$id.done; Remove-Item $win\\touch-$id.txt, $win\\touch-$id.done -ErrorAction SilentlyContinue"
  for i in "${!shots[@]}"; do r="tshot-$id-$((i+1)).png"
    scp -q "$host:C:/setup/restart-tablet/$r" "${shots[$i]}" && echo "shot -> ${shots[$i]}"
    ssh "$host" "Remove-Item $win\\$r -ErrorAction SilentlyContinue"
  done; }
userrun() { local exe="$1"; shift; local a="${*:-}" extra=""
  [ -n "$a" ] && extra=" -Arguments '$a'"
  ssh "$host" "powershell -NoProfile -ExecutionPolicy Bypass -File $win\\userrun.ps1 -Exe '$exe'$extra"; }
stop_all() { ssh "$host" "powershell -NoProfile -ExecutionPolicy Bypass -File $win\\stop.ps1" || true; }

sync_helpers
case "${1:-}" in
  deploy)
    [ -d "$art" ] || { echo "no build at $art; run tools/tablet/build.sh"; exit 1; }
    (cd "$art" && find . -type f -print0 | sort -z | xargs -0 sha1sum) > "$state/manifest.new"
    touch "$state/manifest.deployed"
    changed=$(comm -13 <(sort "$state/manifest.deployed") <(sort "$state/manifest.new") | awk '{print $2}')
    if [ -z "$changed" ]; then echo "deploy[$slot]: nothing changed"; exit 0; fi
    n=$(echo "$changed" | wc -l); (cd "$art" && echo "$changed" | tar -cf "$state/delta.tar" -T -)
    stop_all
    scp -q "$state/delta.tar" "$host:C:/setup/restart-tablet/delta-$slot.tar"
    ssh "$host" "New-Item -ItemType Directory -Force '$dest' | Out-Null; tar -xf $win\\delta-$slot.tar -C '$dest'; if (\$LASTEXITCODE) { exit 1 }; Remove-Item $win\\delta-$slot.tar"
    mv "$state/manifest.new" "$state/manifest.deployed"; echo "deploy[$slot]: $n file(s) updated" ;;
  restart) shift; stop_all; userrun "$dest\\ReStart.exe" "$@"; echo "started ReStart[$slot] $*" ;;
  stop) stop_all; echo stopped ;;
  tablet) m="${2:?on|off|auto}"
    case "$m" in
      on) ps 'New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\ReStart" | Out-Null; Set-Content "$env:LOCALAPPDATA\ReStart\tablet-override.txt" tablet' ;;
      off) ps 'New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\ReStart" | Out-Null; Set-Content "$env:LOCALAPPDATA\ReStart\tablet-override.txt" laptop' ;;
      *) ps 'Remove-Item "$env:LOCALAPPDATA\ReStart\tablet-override.txt" -ErrorAction SilentlyContinue' ;;
    esac; echo "tablet override: $m" ;;
  shot) out="${2:?out.png}"; ssh "$host" "Remove-Item $win\\shot.png -ErrorAction SilentlyContinue" || true
    userrun powershell.exe "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File $win\\shot.ps1 -Out $win\\shot.png"
    for _ in $(seq 1 30); do ssh "$host" "Test-Path $win\\shot.png" | grep -q True && break; sleep 1; done
    scp -q "$host:C:/setup/restart-tablet/shot.png" "$out"; echo "shot -> $out" ;;
  cmd) shift; userrun "$dest\\ReStart.exe" --tablet-command "$@"; echo "sent: $*" ;;
  run) shift; ps "$*" ;;
  userrun) shift; userrun "$@" ;;
  log) ssh "$host" "Get-Content \$env:LOCALAPPDATA\\ReStart\\tablet.log -Tail ${2:-40} -ErrorAction SilentlyContinue" ;;
  session) shift; "$@" ;;
  touch) shift; touch_run "$@" ;;
  testapp) shift; compile_tool testapp; userrun "$win\\testapp.exe" "$@"; echo "testapp started: $*" ;;
  testapp-kill) ps 'Get-Process testapp -ErrorAction SilentlyContinue | Stop-Process -Force'; echo "testapps closed" ;;
  *) sed -n '2,30p' "$0"; exit 1 ;;
esac
