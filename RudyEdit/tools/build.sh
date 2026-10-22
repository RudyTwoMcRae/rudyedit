#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
rid="${1:-linux-x64}"
case "$rid" in linux-x64|win-x64) ;; *) echo 'Usage: tools/build.sh [linux-x64|win-x64]' >&2; exit 2 ;; esac
# Build, publish, stage diagnostics, test the exact native executable, THEN package.
# A foreign target produces a blocked report and stops here. No bypass flag exists.
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet build tests/RudyEdit.Tests -c Release --nologo
dotnet publish src/RudyEdit -c Release -r "$rid" --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -o "dist/$rid" --nologo
python3 tools/stage_release_tests.py "dist/$rid"
if [ "$rid" = win-x64 ]; then exe="dist/$rid/rudyedit.exe"; else exe="dist/$rid/rudyedit"; fi
python3 tests/release_runner.py --executable "$exe"
python3 tests/release_gate.py "dist/$rid"
if [ "$rid" = linux-x64 ]; then
  python3 packaging/manjaro/package.py
else
  python3 tools/verify_icons.py "$exe"
  python3 packaging/windows/build_msi.py
  makensis packaging/windows/setup.nsi
fi
