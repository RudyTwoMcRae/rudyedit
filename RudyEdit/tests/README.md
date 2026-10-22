# Executable release tests (required before installers)

Run these tests after publishing and staging the test runner, before any installer
is built. The native executable launches the managed diagnostics itself through
`--self-test`; tests bind to the assembly already loaded by that executable.
Linux terminal suites also launch that executable in real pseudo-terminals.
Windows tests additionally open a dedicated console and exercise the Win32 input
and output APIs, function/control keys, mouse records and restoration.

## Run the installed/portable tests

Python 3 is required for diagnostics, protocol fixtures and release reporting.
The editor itself does not require Python unless you use Python development tools.
On Linux install the test-only dependencies with:

```
python3 -m pip install -r tests/requirements.txt
python3 tests/release_runner.py --executable ./rudyedit
```

On Windows, from the application folder:

```
python tests/release_runner.py --executable .\rudyedit.exe
```

Use `--report /writable/folder/RudyEdit-tests.log` when the application is installed
in Program Files or /opt and the default report folder is not writable. The
Run-Tests.cmd / Run-Tests.sh convenience scripts write results in your current
user configuration folder. Tests run automatically on the published executable
during release builds. A source checkout first builds the test DLL and stages it
with tools/stage_release_tests.py; the shipped application already has it.

The text report begins with Tests performed, Passed, Errors and Blocked. It then
lists every failure/error/block, including exception details, followed by all
named tests/checkpoints. A JSON companion retains the same records, suite list,
platform, UTC timestamps, command coverage and SHA-256 of the tested payload.
Each named case can contain multiple assertions; keyboard matrix loops are
reported as case groups rather than claiming each iteration is a separate test.
Subprocess output is retained beside the report as *.output.txt. Expected errors
in negative tests are checked in temporary app logs, not counted as failures.

## Coverage and limits

- Every distinct command in the executable catalog has an outcome test in both
  Code and VIM modes. It checks state, persisted bytes, popup contents, subprocess
  output or debugger/protocol events; opening a menu alone is insufficient.
- The catalog comparison fails if a future command lacks a registered/passing
  outcome test. File Manager commands and commands without default shortcuts use
  physical-key chords loaded from a temporary XML map through ordinary dispatch.
- Additional cases cover documents, undo/redo, save conflicts and encodings,
  Unicode, settings, keymaps, tag migration/storage, hidden identifiers, error log
  detail/fallback/rotation, all icon glyphs, layouts, mouse and narrow terminals.
- Build/run and source debugger command outcomes use real Python subprocesses.
  C/C#/Rust build plans, diagnostic parsing and debugger/LSP/DAP protocol logic
  are tested separately. LSP/DAP use bundled deterministic protocol fixtures.
  Optional third-party compilers, servers and adapters are not all installed;
  these results do not certify every external tool/version or project setup.
- Linux suites replay legacy and Ghostty-style Kitty encodings. These are real
  PTY tests, not a running Ghostty GUI/font-renderer certification. On Windows,
  managed UI scripts and native console API tests cover their respective layers.
- Tests use temporary files/configuration; they never overwrite user settings or
  documents. External opener tests use a stub rather than opening desktop windows.
- Platform blocks, timeouts and missing Python/dependencies count against release
  readiness. A foreign-platform executable is never treated as tested/passed.

## Build and packaging policy

`tools/build.sh` defaults to linux-x64; `tools/build.ps1` defaults to win-x64.
Both build → publish → stage tests → run against that native executable → gate.
Use separate native hosts for the two targets. No skip-test switch is provided.
Direct MSI/pacman builders and the NSIS script repeat the gate. It requires a
successful report at `tests/results/release-tests.json` under the publish folder
for the exact bytes being packaged. Changing any payload file requires retesting.
Reports themselves and Python bytecode caches are excluded from the hash.

A Windows native test run is required before a Windows installer can be built.
A Linux pass cannot authorize a Windows installer. Copy the tested publish tree,
including its JSON receipt, to a packaging host if msitools/NSIS are on Linux.
The gate can check that receipt without executing the foreign binary. This is a
build-quality guard, not a cryptographic signature from an independent lab.
