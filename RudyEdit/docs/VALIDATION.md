# Release validation — 2.0.5

Build host: Linux x86_64 (Ubuntu 24.04 environment), .NET SDK 10.0.100. Release publish runtime: .NET 10.0.13 for linux-x64 and win-x64.

## Automated checks

The console test harness uses no third-party test framework or NuGet packages. The original suite has 38 checks covering (see the 1.0.1 audit below for the expanded suite):

- Document editing/selection, undo/redo, Unicode grapheme deletion, line navigation, formatting, lists, find/wrap.
- UTF-8 BOM and CRLF round trips, dirty state, external-change protection, existing-destination protection, binary rejection.
- Validated config parsing and supplied mode conditions; invalid config rejection.
- Supplied XML chord/context handling and invalid command rejection.
- Language detection, local completion, English spelling/suggestions, terminal key decoding and escape sanitization.
- Windows ConsoleKeyInfo normalization: all four arrows with all eight modifier combinations, Ctrl+Q, every Alt menu accelerator, and ordinary text.
- Ghostty/Kitty flag 1 decoding: Ctrl+Q, Alt menu shortcuts, Escape, arrows, F2, and Caps Lock/Num Lock modifier combinations.
- F1–F4 with omitted/default CSI parameters, SS3 and tilde encodings; Ctrl+Shift+W from Windows/Linux input and default XML, including existing configuration fallback.
- Split layout, safe file copy/move, destination collision and self-copy guards.
- Build plan/project discovery and compiler diagnostic parsing.
- Real subprocess execution and line input.
- Real Python debugger stepping, breakpoint stop, and local values.
- LSP stdio framing, completion ranges in UTF-16 coordinates, and live diagnostic events against a protocol fixture.
- DAP stdio request/response handling, asynchronous stopped events, and stack frames against a protocol fixture.
- Presence of offline guides and bundled font.

Run:

```sh
dotnet build tests/RudyEdit.Tests -c Release
dotnet run --project tests/RudyEdit.Tests -c Release --no-build
```

## Terminal smoke test

`tests/terminal_smoke.py` drives a real Linux pseudo-terminal at 120×36 with the published executable. Sixteen assertions cover initial UI, no repaint while idle, bracketed paste/Unicode, saving to disk, duplicate-document splits, pane navigation, Vim insert/undo, mode changes, enhanced Shift+Space, configuration and shortcut help, menu access, and clean terminal restoration. The terminal grid was also visually inspected. The optional script requires Python's pyte and Pillow packages; they are test dependencies, not application dependencies.

## Distribution checks

The Windows executable is a PE x64 application; the installer wrapper is a Windows GUI NSIS launcher containing an x64 MSI. The MSI database is inspected for product/upgrade identity, target architecture, installed files, Start Menu/Desktop shortcut records, Desktop condition, font registration, PATH environment entry, and standard install/uninstall actions. Embedded application files are extracted and compared to their publish output.

The Manjaro distribution is a native `.pkg.tar.zst` archive. Its metadata, executable launcher, application entry, font, runtime, licenses, and file bytes are inspected. The packaged Linux application is executed and exercised from a temporary extraction rather than installed on the host.

## Limits of verification

No native Windows or Manjaro machine was available. UAC, Windows console rendering, font registration, actual native installation, upgrades, and package-manager uninstall have not been run on those platforms. The Linux terminal tests cannot prove that every terminal renders without flicker.

Python debugging was exercised against real code. C# netcoredbg initialization and launch/breakpoint protocol exchange were attempted, but the adapter did not complete configuration/startup on this host; actual C# stepping remains unverified. C/Rust gdb execution and real third-party language-server compatibility were not exercised here. Protocol fixtures test the client implementation, not these external tools. Test the optional native adapters with your installed toolchain versions before relying on them.

An opt-in C# test is included for a suitable machine: build `tests/fixtures/DebugTarget` in Debug, set `RUDYEDIT_DAP_FIXTURE_DLL` and `RUDYEDIT_DAP_FIXTURE_SOURCE` to its absolute DLL/source paths, set `RUDYEDIT_NETCOREDBG` if necessary, and run the harness. It is deliberately separate from the standard suite.

## Windows installer revision 2 — error 2235

The original MSI's manually authored Font table had an incorrect primary column named `Font` instead of `File_`. A query for `File_` failed against that package. The corrected builder uses the Microsoft schema, leaves FontTitle null for the TrueType font, and runs `packaging/windows/validate_msi.py` after constructing each MSI. The validator checks the Font/Environment schemas, referenced files/components, required install/uninstall actions, and actual registration SQL queries. The old MSI is rejected by this regression check; the corrected MSI passes. Both Windows distributions and the source ZIP have been rebuilt.

This fixes the demonstrated package defect. A full Windows 11 install/uninstall test is still required; no native Windows machine is available on this build host.

References: https://learn.microsoft.com/windows/win32/msi/font-table and https://learn.microsoft.com/windows/win32/msi/windows-installer-error-messages

## Windows keyboard revision 3

Windows arrow events now use the same canonical names as the editor, dialogs, and file manager. Alt+F/E/M/T/V/B/H opens the corresponding menu in both keybinding modes (Build requires Development mode). Ctrl+Q takes priority over dialogs, pending chords, Vim command entry, and focus. It cancels any suspended dialog action before checking unsaved documents. Repeated quit requests do not bypass Save/Discard/Cancel. Windows console input/output modes and Ctrl+C handling are restored on disposal, with terminal cleanup protected by finally blocks.

The 31 core checks pass with zero compiler warnings. `tests/keyboard_smoke.py` passes 90 assertions across 12 Linux PTY sessions, covering Code/VIM navigation, Vim Insert/Visual and pending operators, all seven menus, pending chords, modal Save As interruption, help, file-manager/console focus, Vim command entry, Save/Discard/Cancel, repeated quit, and restored terminal settings. The existing 16-check terminal smoke suite also passes. Run the keyboard suite with Python and pyte, optionally setting `RUDYEDIT_TEST_EXE` to an executable path.

The Windows MSI and setup EXE retain the revision 2 Font-table correction and were rebuilt with the updated app. Native Windows 11 key delivery, console restoration, and installation still require verification on Windows; the Windows key decoder is exercised cross-platform using real ConsoleKeyInfo values. That Windows release did not update the Manjaro package; the Linux update below does.

## Manjaro package release 2 — Ghostty keyboard checks

`rudyedit-1.0.0-2-x86_64.pkg.tar.zst` brings the shared revision 3 keyboard fixes to Linux: Alt menus and global Ctrl+Q work in Code and VIM modes, including during modal dialogs and pending shortcuts. The package release increases from 1 to 2 so pacman recognizes the update. The application runtime remains .NET 10.0.13.

The test suite can replay the same interactions as both legacy terminal input and Kitty flag 1 input (the enhancement requested by RudyEdit and supported by Ghostty). The enhanced variant sets TERM=xterm-ghostty, encodes Alt and Ctrl shortcuts as CSI-u sequences, and checks that software flow control is off so Ctrl+Q reaches the app. Both variants check that the keyboard mode, cursor, alternate screen, and terminal settings are restored on exit.

Run against an extracted package, with Python and pyte installed:

```sh
RUDYEDIT_TEST_EXE=/absolute/path/to/extracted/opt/rudyedit/rudyedit python3 tests/keyboard_smoke.py
RUDYEDIT_KEY_PROTOCOL=kitty RUDYEDIT_TEST_EXE=/absolute/path/to/extracted/opt/rudyedit/rudyedit python3 tests/keyboard_smoke.py
```

Validation passed: 32 core checks, 90 legacy and 90 Kitty keyboard/quit checks across 24 PTY sessions, and the existing 16-check terminal smoke suite. Tests ran against the executable extracted from the package; all 225 application payload files match the publish output. Package metadata, executable permissions, the launcher, and bundled font were also verified.

Ghostty's graphical application and Manjaro's pacman installation were not available on the build host. Protocol replay verifies the packaged application's input handling, but does not exercise desktop-level or user-configured Ghostty keybindings.

Protocol references: https://ghostty.org/docs/features and https://sw.kovidgoyal.net/kitty/keyboard-protocol/

## Historical function-key fix and Close Panel shortcut (superseded in 1.0.3)

The previous Ghostty simulation always sent explicit `CSI 1;1P`/`CSI 1;1Q` for F1/F2. Ghostty's keyboard protocol also permits omitted defaults: `CSI P`/`CSI Q` and `CSI 1P`/`CSI 1Q`. Those forms were missing from the decoder. The decoder now accepts both forms for F1–F4. The Kitty smoke test now uses omitted defaults so it detects this regression.

That earlier release assigned Ctrl+Shift+W to Close Panel on both platforms and in Code/VIM modes. Version 1.0.3 replaces that assignment with Close Tab; Ctrl+W closes panels. It is listed in the bundled KeyboardShortcuts.xml, command menu, and built-in defaults used when an older user XML lacks the shortcut. User bindings are retained. Closing a pane checks all unshared unsaved tabs; the last pane resets to a blank document instead of exiting.

Validation: 34 core checks pass with zero build warnings. `tests/function_panel_smoke.py` passes 57 checks per input format (legacy and Kitty), exercising four F1/F2 encodings, both keybinding modes, current-panel selection, Save/Discard/Cancel, preservation of another panel, and last-panel behavior. Windows installer revision 4 and Manjaro package release 3 contain these changes. Native Windows and a live Ghostty graphical session remain outside the build host's test coverage.

## Mouse support and menu/tab updates

Windows installer revision 5 and Manjaro package release 4 remove the menu-bar hint and underline each main menu's first letter. The default XML now uses Ctrl+Alt+Left/Right Arrow for previous/next tab; existing user XML is retained.

Linux enables SGR button-motion reporting (1002/1006), decoding press, release, drag and wheel events. Windows reads native KEY_EVENT/MOUSE_EVENT records, disables Quick Edit while running, preserves key repeats, and restores prior input/output modes on disposal. Mouse tracking is disabled on exit. Shared hit testing supports menus and commands, popup dismissal, tabs and panel focus, cursor placement, drag/Shift-click selection, and wheel scrolling. Grapheme-aware cell mapping handles tabs, wide glyphs, surrogate pairs and combining characters.

The 38 core checks cover SGR event parsing, malformed mouse packets, Windows record layout/key/mouse decoding, coordinate offsets, Unicode hit testing, and the new default XML. `tests/mouse_smoke.py` exercises the real PTY input loop in Code and VIM modes, checks menu underline attributes, clicks every main menu, runs menu commands, selects tabs with mouse and keyboard, copies/reuses drag and Shift-click selections, positions within Unicode text, scrolls without moving the caret, focuses/closes split panels, cancels/discards quit dialogs with the mouse, and verifies terminal mouse-mode cleanup.

Native Windows console event delivery and a live Ghostty window still require platform testing. Cross-platform tests exercise the Windows record decoder; Linux PTY tests replay real SGR mouse packets, not desktop automation.


## Windows input audit — version 1.0.1 (2026-10-21)

### Reproduced defect

The 1.0.0 Windows startup sent the Kitty flag-1 request (`ESC [ > 1 u`) even though Windows read individual `KEY_EVENT_RECORD` values without assembling terminal sequences. Replaying `ESC [ 113 ; 5 u` through that reader yielded Escape followed by the literal text `[113;5u`; F1 (`ESC O P`) yielded `OP`. This reproduces the reported shortcut text corruption. The exact terminal host on the user's machine has not been inspected.

### Corrections

- Windows explicitly selects legacy terminal keyboard encoding inside its alternate screen; native `ReadConsoleInputW` provides key modifiers and mouse events. Bracketed paste is requested only by the Unix input path. Existing terminal protocol state is popped on exit.
- A streaming decoder also accepts character-record CSI/SS3 and CSI-u input on Windows. It assembles split sequences, consumes unsupported sequences without inserting their payload, preserves paste boundaries and surrogate pairs, and handles native repeat counts and key-up filtering.
- Ctrl/Alt commands carry no insertable text, including inside search prompts and the command console. Native Ctrl+Q is never swallowed by a pending partial sequence. AltGr composed text is retained.
- Decoder validation handles oversized numbers, invalid Unicode values, release events and unsupported functional keys. CSI-u F keys, modifier/event fields, alternate uppercase text and xterm modifyOtherKeys are recognized.
- Windows mode setup checks every API result. Processed input, line/echo input, Quick Edit and VT input are disabled; native mouse/window events are enabled. Input/output modes, output encoding, cursor and screen are restored on disposal, including startup failures.
- A failed external command launch no longer leaves an unstarted Process that can make Quit throw. Malformed compiler line/column numbers are ignored safely. Cleanup attempts all resources even if another resource fails.
- Creating a new document and hiding the console return focus to the editor.
- The release version is now 1.0.1 in the application and Windows installers. The stable MSI UpgradeCode and Font-table error-2235 fix are retained.

### Validation scope

`WindowsInputTests.cs` runs the production Windows stream/record decoder into the actual editor command loop, using an injected screen/input device rather than Windows OS APIs. It exercises native, CSI-u and legacy encodings, both Code and VIM modes, shortcut actions, Save As, multiple quit/cancel contexts, repeated quit, and terminal disposal. It also tests all F1–F24 modifier combinations, left/right modifiers and locks, Ctrl+A–Z, arrows, repeats, Unicode, paste and malformed input. F12 has no default command; its decoding is verified without inventing a new action. F11/Shift+F11 require a paused debugger to step.

A separate `--windows-input` test invokes real WriteConsoleInputW/ReadConsoleInputW and checks console mode restoration when run on Windows:

```powershell
dotnet run --project tests/RudyEdit.Tests -c Release -- --windows-input
```

Run this in a dedicated interactive Windows console. It intentionally clears that test console's pending input. It has not been run on this Linux build host and is not counted as a passing test. Native Windows Terminal/ConHost delivery, Windows 11 installation/UAC, upgrade/uninstall and third-party debugger integration still require native platform verification. Automated checks cannot establish that the application has no remaining defects.

Protocol/API references:
- https://learn.microsoft.com/en-us/windows/console/key-event-record-str
- https://learn.microsoft.com/en-us/windows/console/setconsolemode
- https://sw.kovidgoyal.net/kitty/keyboard-protocol/


### Completed results for this build

| Check | Result |
|---|---|
| Release build | Passed; zero compiler warnings and errors |
| Core and expanded Windows input/editor suite | 106 test groups passed, zero failed |
| Windows input editor scenarios | 60 scenarios across native, CSI-u and legacy input, Code and VIM modes |
| Function-key decoding matrix | F1–F24, all eight Ctrl/Alt/Shift combinations, both modifier sides and lock states |
| Malformed input resilience | 10,000 generated CSI-u inputs plus explicit overflow/invalid-Unicode cases; no exceptions |
| Published app keyboard/quit PTY tests | 90 legacy + 90 Kitty checks passed |
| Published app mouse/menu/tab PTY tests | 71 checks passed |
| Published app function-key/panel PTY tests | 57 checks passed |
| Published app terminal smoke test | 16 checks passed |
| MSI structural/registration validation | Passed; 226 file records, Font and Environment tables, two font-registration SQL checks |
| MSI payload extraction | All 225 application files match the publish directory byte-for-byte |
| MSI upgrade identity | ProductVersion 1.0.1; permanent UpgradeCode preserved |
| Native Windows OS/API test | Not run; source harness included |
| Native Windows 11 install, upgrade, uninstall and terminal host | Not run; requires Windows |

The 324 PTY checks use Linux to exercise the shared editor and actual terminal stream. They do not substitute for native Windows testing. The Windows-specific tests use Windows record layouts and production decoder/dispatch code on the build host. Test results are evidence for the covered behaviors, not a guarantee that all possible defects are absent.


## Historical version 1.0.2 — Ctrl+W panel removal (shortcut alias superseded)

Version 1.0.2 assigned Ctrl+W to Close Panel rather than Close Tab and retained Ctrl+Shift+W as an alternate panel shortcut. In 1.0.3, Ctrl+Shift+W instead closes the active tab. Legacy explicit Close Tab/Ctrl+W entries are interpreted as Close Panel without rewriting the user's XML. Close Tab remains available through the File menu.

Validation: 123 test groups passed, zero failed; Release build had zero warnings/errors. Sixteen new editor integration scenarios cover both shortcuts, Windows native records and CSI-u input, Code/VIM modes, and vertical/horizontal splits. Each scenario checks Cancel retains the panel, Save writes one unsaved tab, Discard handles another, the panel is removed, and the other panel's shared document remains available. A defaults/migration regression checks both bindings with empty, bundled, and legacy XML. The final remaining panel retains the existing behavior of resetting to a blank document; Ctrl+Q exits the application.

Windows version is 1.0.2. The permanent UpgradeCode and MSI Font-table fix are retained. Native Windows 11 installation and terminal event delivery remain unverified on this Linux build host.


## Version 1.0.3 — distinct tab and panel shortcuts

Current assignments: Ctrl+Shift+W (Shift+Ctrl+W) closes and removes the active tab; Ctrl+W closes the current panel; Ctrl+Q quits. Closing a tab selects a remaining tab and preserves both the panel and all other tabs. The last-tab fallback remains one blank document. Save/Discard/Cancel protection and shared-document handling remain in place.

The bundled and specification XML files, README, feature coverage, menu specification, description addendum, CLI help, in-app command catalog, and packaging instructions use these assignments. On startup, legacy Close Panel/Ctrl+Shift+W and Close Tab/Ctrl+W entries are migrated atomically in the user's XML; comments and unrelated bindings are retained. The loader also recognizes the new meanings if the file is read-only or old bindings are reintroduced.

Regression scenarios cover native Windows records and CSI-u input, Code/VIM modes, vertical/horizontal splits, cancellation, saving/discarding the active tab, removing a middle and final-position tab, selecting remaining tabs, and retaining other panels. XML migration is checked for idempotence and preservation of comments/custom bindings. Native Windows 11 terminal and installer execution remain outside this Linux build host's verification.

Completed 1.0.3 validation: 132 test groups passed, zero failed; Release build completed with zero warnings/errors. The MSI schema/registration validator passed, and all 225 application files extracted from the MSI match the publish output. Both source XML files and the MSI-bundled XML map Ctrl+Shift+W to Close Tab and Ctrl+W to Close Panel.


## Version 2.0.0 — Text bars

The Text-mode formatting row exposes all existing formatting operations and compact font, size, and colour selectors. The bottom current-file tag display and inline Add tags field use the same metadata implementation as the Tags editor. File Manager search lists saved files matching all entered tags. Both supplied keybinding files include Ctrl+Alt+M (formatting), Ctrl+Alt+T (add tags), and Ctrl+Alt+G (search). Existing installations also recognize these shortcuts immediately. Development mode hides all three bars.

Validation: Release builds treat warnings as errors. 136 automated test groups passed, including Windows native-record/encoded input and tab/panel regressions plus tag metadata, undo, selection preservation, saved-file search, per-tab entry drafts, mouse/keyboard formatting, Code/VIM use, small terminal drawing, and Development-mode hiding. Native Windows execution was unavailable on this Linux host; Windows console records and encoded streams were tested through the production input reader and editor loop.

Real Linux PTY checks passed: 14 Text-bar checks under each of legacy and Ghostty-compatible Kitty key encodings; 71 mouse/menu/tab checks; 57 function-key/panel-close checks; 90 keyboard/quit checks across 12 terminal sessions. These test terminal protocols, rather than a launched Ghostty GUI. The MSI validator checks required Font/Environment tables, file references, upgrade sequencing, and font registration SQL. The Windows executable installer and Manjaro pacman package are built from self-contained .NET 10.0.13 publishes. Actual Windows installation and Manjaro pacman installation remain unverified on this host.


## Version 2.0.1 — External tag registry and tag-bar toggle

Ctrl+Shift+T toggles the current-file tag bar in Text mode; Ctrl+Alt+T reveals and focuses it. The two supplied keybinding XML files, command catalog, in-app Help, configuration guide, README, feature matrix, Markdown manual/generator, packaging notes, and specification documents include the new shortcut and Tags.xml behavior. ShowTagBar controls startup visibility.

Tags.xml stores tag names by UUID. Saved text contains only a leading identifier comment, hidden from Document.Text and consequently from editing, selection, clipboard, and preview. Opening an old leading tags header stages migration; saving removes its names from the file and commits XML metadata. Unsaved tags remain in document state and participate in dirty checks and undo/redo. Save As assigns an independent UUID and rebases undo/redo identities; external moves retain the embedded UUID. Search resolves saved-file IDs through the registry, with legacy-header compatibility.

The automated suite contains 145 test groups. Added coverage includes XML escaping and schema validation, UTF-8 BOM/CRLF preservation, metadata-only changes and discard, migration without removing body blank lines or documentation examples, saved file search, rename and Save As independence with undo, concurrent/stale edits, rollback after file replacement failure, corrupt-registry protection, and registry self-tagging/replacement prevention. Production Windows native-event, CSI-u, and legacy-input editor flows test the new toggle. The Text-bar keyboard/mouse tests cover both Code and VIM modes.

Real PTY checks: 18 checks with legacy encoding and 18 with Ghostty-compatible Kitty encoding exercise the toggle, XML-backed save, hidden identifier, tag search, and exit. The 57 function-key/panel-close checks also pass. Windows and Linux x64 publishes bundle .NET 10.0.13; the Windows MSI retains the permanent UpgradeCode and standard Font table. Native Windows installation/execution and actual Manjaro package installation are unavailable on this Linux host.

Tag persistence uses atomic registry replacement under a cross-process lock, merges other files' records, and rejects conflicting values for the same file. A metadata-write failure leaves the document unchanged; a failed document replacement restores the previous XML while holding the lock. The file and XML are separate filesystem objects, so a process/system crash between commits is not a multi-file atomic transaction. Back up Tags.xml together with tagged documents.


## Version 2.0.2 — Binder and fountain-pen icon

Release compilation treats warnings as errors. The same 145 automated test groups verify editor/input/tag functionality after the version and resource changes. The artwork is an SVG with three ring groups, an outlined R, and a vector fountain pen, with no text/font dependencies or embedded raster content. Rendered artwork is visually inspected; transparent PNG/ICO derivatives are generated with Inkscape and Pillow.

The Windows executable and Setup are checked with tools/verify_icons.py: every 16/24/32/48/64/128/256 px RT_ICON payload must match the source ICO and be linked from a complete RT_GROUP_ICON. MSI validation checks Icon table data, shortcut references, and ARPPRODUCTICON alongside the existing font/environment/reference checks. Manjaro archive checks verify that the SVG and PNG bytes match the master derivatives and that the desktop entry selects the rudyedit icon.

Both self-contained x64 publishes use .NET 10.0.13. Actual Windows installation/rendering and Manjaro desktop rendering are not available on this Linux build host; the validation checks compiled resources and package contents. Terminal tabs/windows remain controlled by the terminal host.


## Version 2.0.3 — Persistent errors and single Windows Setup

Release builds treat warnings as errors. The suite contains 161 test groups, including complete concurrent log records, exception stacks/nested errors, UTC context, 5 MiB rotation with three backups, unwritable-config fallback, real tool stderr/diagnostic/nonzero-exit capture, malformed asynchronous protocol logging, Python-debugger stderr/exception logging, safe folder-opening arguments, six production Windows native/legacy/CSI folder-shortcut flows, command-error logging, and configuration-fallback logging. Existing editor, input, tab/panel, tags, LSP, and debugger checks remain covered.

The app keeps a local bounded error log, captures unhandled managed and unobserved-task errors through global hooks, and logs errors consumed by recovery paths. Cancellation and expected cleanup races do not create error entries. A failed log write tries the temporary fallback; failure of both destinations cannot be persisted and is reported to the user. Help → Error Log reads a snapshot with sharing enabled.

The only public Windows package is Setup EXE. Its internal MSI lives at dist/windows-internal/RudyEdit.msi and is embedded into Setup. MSI tables/font/icon/upgrade references and the EXE's seven icon frames are validated. Both self-contained x64 publishes use .NET 10.0.13. Native Windows install/rendering and actual Manjaro package installation remain unavailable on this Linux host.


The Linux PTY integration check passed 9 checks under legacy terminal encoding and 9 under Ghostty-compatible Kitty encoding. It verifies that Ctrl+Alt+C passes the exact folder path to the desktop opener, exposes the folder in the in-app File Manager, logs a deliberately failing opener without exiting, and opens the searchable error-log snapshot. A separate CLI invocation verifies that a fatal argument error exits with code 1 and writes the exception to the log. The Windows Setup also records failed-install callbacks in its verbose installation log and checks failure to start Windows Installer. Manjaro declares xdg-utils as an optional dependency for desktop folder opening.

## Version 2.0.4 — Nerd Font UI validation

- Release build: zero compiler warnings/errors; **171 automated test groups passed**. New cases exercise file/folder classification, special-name precedence, one-cell private-use glyphs, validated UseNerdIcons defaults/fallback, icon mouse targets and font help in Code/VIM, and formatting keyboard access at 20/40/80/120 columns.
- `python3 tools/verify_nerd_font.py` checks all **78 catalog glyphs** against the bundled TrueType cmap and horizontal metrics using only Python standard-library code. Every glyph is mapped in the font, lies in BMP private use, and has the same 1,200-unit advance as an ordinary text cell. The font itself is unmodified FiraCode Nerd Font Mono / Nerd Fonts 3.4.0, with original licenses.
- Real PTY checks passed with both legacy xterm input and Ghostty-style Kitty reporting: **64 icon checks**, **71 mouse/menu/tab checks**, **18 Text-bar checks**, and **90 keyboard/quit checks per protocol** (486 checks total). Icon scenarios cover special folders, file types, folder traversal, clicking a file glyph, menu glyph hit targets, underlined initials, formatting a selection, plain-label toggle, font help, 20/40/80/120-column dropdown access, and terminal restoration. The PTY harness decodes UTF-8 incrementally so a glyph split across reads is not misreported as a replacement character.
- Published both self-contained x64 builds with .NET 10.0.13; the source targets .NET 10. Windows EXE/Setup icon resources and internal MSI schemas/references/font registration were inspected. Manjaro archive metadata, font, licenses, help and executable were inspected. Public Windows distribution remains a single Setup EXE.
- Host terminal font selection remains a user setting; installation registers/installs the font but does not modify terminal profiles. Windows Setup explains font selection on its finish page. Help → Nerd Font & Icons includes instructions, official terminal documentation links and the complete legend. Ordinary-font users have a session toggle and persistent UseNerdIcons=false fallback.

Native Windows execution/install/upgrade/uninstall and a real Manjaro/Ghostty GUI rendering session were unavailable on this Linux build host. Native input decoding is regression-tested, but binary inspection and PTY tests do not replace those platform checks.

## Version 2.0.5 — Packaged executable diagnostics and enforced release gates

The Linux self-contained 2.0.5 executable was launched by tests/release_runner.py after the final publish and staging steps. Its --self-test entry point loaded the shipped runner against its own application assembly (verified by an assertion), rather than testing only a separately built source assembly. **981 named tests/checkpoints passed; 0 errors; 0 blocked.** The 339 managed groups include verified outcomes for every one of the **83 distinct command IDs in both Code and VIM modes**, plus the existing document, Unicode, settings, keybindings, tags, logs, icons and protocol regressions. Real subprocesses exercise Python build/run, shell output, debugger continue/step-over/into/out/locals/stop and LSP/DAP fixture exchanges.

Linux terminal tests use the actual executable in PTYs with both legacy and Ghostty-style Kitty input. Required suites are keyboard/quit, function keys/panel close, mouse/menu/tab handling, Text bars, graphic icons, config-folder/logging and terminal smoke. The report retains the complete named-case/checkpoint list and individual subprocess output. Font glyph metrics, CLI behavior, fatal-error logging and gate regression tests also passed. Core matrix iterations are case groups, not inflated into separately counted assertions.

Packaging guard regression checks rejected missing/failing reports, wrong targets, missing native suites, uncovered commands and altered executable/scripts. Direct Windows MSI and NSIS entry points were also invoked with the blocked Windows receipt: both refused packaging before changing/creating output. The old internal MSI was not overwritten, and no 2.0.5 Windows Setup EXE was created. The Windows EXE's seven binder-and-pen icon frames were inspected separately.

The win-x64 executable and all scripts were published/staged, but native execution is unavailable on this Linux host. Its report records **0 performed, 0 passed, 0 errors, 2 blocks**, identifies the platform mismatch and missing mandatory native suites, and remains not release-ready. A Windows validation ZIP is supplied for running tests/Run-Tests.cmd on Windows with Python 3. A native pass for the same bytes is required before building the Windows installer. This restriction applies to this revision and all later revisions.

Manjaro packaging ran only after the native Linux pass and a successful exact-payload gate. The resulting package contains the tested executable, test DLL/sources/scripts, protocol fixtures, convenience launcher, font audits, test instructions, and completed text/JSON reports. Documentation and all installer/build entry points record the build → publish → stage → native executable tests → gate → installers requirement; AGENTS.md persists it for future work. Reports/checkpoints start with totals, list every error/block, then the full test list. Payload changes invalidate the receipt.

These results do not replace native Manjaro installation or Ghostty GUI/font-renderer checks, a native Windows install/upgrade/uninstall test, or compatibility testing against every optional third-party compiler/server/debugger. Those limits are stated in tests/README.md and the reports.
