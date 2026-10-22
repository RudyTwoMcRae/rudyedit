#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
rid="${1:-linux-x64}"
case "$rid" in linux-x64|win-x64) ;; *) echo 'Usage: tools/build.sh [linux-x64|win-x64]' >&2; exit 2 ;; esac
# Build, publish, stage diagnostics, test the exact native executable, THEN package.
# Windows requires a completed native attempt; failures remain visible while packaging proceeds.
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet build tests/RudyEdit.Tests -c Release --nologo
dotnet publish src/RudyEdit -c Release -r "$rid" --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -o "dist/$rid" --nologo
python3 tools/stage_release_tests.py "dist/$rid"
if [ "$rid" = win-x64 ]; then exe="dist/$rid/rudyedit.exe"; else exe="dist/$rid/rudyedit"; fi
test_status=0
python3 tests/release_runner.py --executable "$exe" || test_status=$?
if [ "$rid" = win-x64 ]; then
  python3 tests/release_gate.py "dist/$rid" --allow-test-failures
else
  python3 tests/release_gate.py "dist/$rid"
fi
if [ "$rid" = linux-x64 ]; then
  python3 packaging/manjaro/package.py
else
  python3 tools/verify_icons.py "$exe"
  python3 packaging/windows/build_msi.py
  makensis packaging/windows/setup.nsi
fi

exit "$test_status"
