#!/usr/bin/env bash
set -euo pipefail
test_dir="$(cd -- "$(dirname -- "$0")" && pwd)"
config_dir="${RUDYEDIT_CONFIG_HOME:-${XDG_CONFIG_HOME:-$HOME/.config}/RudyEdit}"
exec python3 "$test_dir/release_runner.py" --executable "$test_dir/../rudyedit" --report "$config_dir/test-results/RudyEdit-tests.log"
