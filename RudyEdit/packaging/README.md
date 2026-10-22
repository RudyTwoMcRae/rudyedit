# Packaging and upgrades

Application version: 2.0.5. The source targets .NET 10 and release binaries bundle the .NET 10.0.13 runtime. Keep runtime servicing current when producing later releases.

## Publish

From the project root, with .NET 10 SDK installed:

```sh
dotnet publish src/RudyEdit -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -o dist/win-x64
dotnet publish src/RudyEdit -c Release -r linux-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -o dist/linux-x64
```

No third-party NuGet libraries are used by the editor. Publishing downloads Microsoft's runtime packs if not cached. Copy all files in a publish directory. Build/release scripts require network only when dependencies are not cached.

## Single Windows installer

On a Linux build host, install Python 3, `wixl` (msitools), and `makensis` (NSIS), then:

```sh
python3 packaging/windows/build_msi.py
makensis packaging/windows/setup.nsi
```

**Installer revision 2 (Windows error 2235 fix):** The Font table uses the required `File_` column and reads the TrueType font title from the font itself. The builder now validates authored table schemas, foreign keys, and registration SQL before succeeding. Application version remains 1.0.0; newly generated ProductCode and PackageCode identify the corrected installer.

**Windows revision 3 (keyboard fixes):** Both Windows installers include canonical arrow-key handling, Alt menu accelerators, and global Ctrl+Q with unsaved-change protection and terminal restoration. The revision 2 MSI correction is retained. The source ZIP includes the Windows key-decoding and terminal interaction regression tests.

The build script uses wixl and then imports the font, PATH, and optional Desktop condition tables with msitools, because wixl does not support all corresponding WiX elements. Keep this postprocessing step when rebuilding; the generated .wxs alone omits these features. The MSI installs under Program Files, appends the install folder to system PATH, installs the font into FontsFolder, and creates a Start Menu entry. The NSIS wrapper invokes that same MSI, offers an optional Desktop shortcut, and writes a verbose log to `%TEMP%\RudyEdit-install.log`. The only public Windows artifact is `RudyEdit-2.0.4-Windows-Setup.exe`, which embeds all installation files and requires no companion download. The MSI is a build intermediate at `dist/windows-internal/RudyEdit.msi` and is consumed by Setup; do not publish it as a second installer.

The MSI UpgradeCode is permanently `D4984166-4052-47D1-90B0-8F5C320B086C`. File component GUIDs are deterministic by relative path. For a later release, increment the version in the csproj, generator, setup script, package generator, build scripts, and output names; retain UpgradeCode. MajorUpgrade removes earlier products. Same-version reinstall is allowed; lower-version installers are blocked. Per-user config is outside the MSI and survives upgrades/uninstall.

Release installers are unsigned. Sign the MSI and EXE with your own trusted code-signing identity for public distribution. Windows execution, UAC, installation, upgrade, and uninstall need testing on a real Windows machine; they cannot be fully validated by Linux artifact inspection.

## Manjaro / pacman

Historical Manjaro package 1.0.0 release 4 included mouse menus, selection and navigation, underlined menu initials, Ctrl+Alt+Left/Right Arrow tab defaults, the previous F1–F4 fix, and the former Ctrl+Shift+W panel-close assignment. Current source uses Ctrl+Shift+W for tabs and Ctrl+W for panels. It upgrades earlier package releases using the same package name and preserves the user's configuration. Built-in shortcuts remain available with existing XML unless the key combination has been customized.

Windows installer revision 5 includes the same application/default-binding changes and native console mouse input, and retains the MSI Font-table correction. Application version remains 1.0.0; new ProductCode and PackageCode identify the rebuilt installers.

```sh
python3 packaging/manjaro/package.py
sudo pacman -U dist/rudyedit-1.0.0-4-x86_64.pkg.tar.zst
sudo pacman -R rudyedit
```

The package generator requires Python 3 and zstd. It writes a native pacman package with .PKGINFO, dependency declarations, the self-contained application under /opt/rudyedit, a /usr/bin launcher, application-menu entry, icon, font, and licenses. Pacman's fontconfig hooks refresh the font cache. Use pacman -U on later versions to upgrade; do not install by extracting the package onto the filesystem. Validate installation/update/removal on a Manjaro system before wider deployment.


## Version 1.0.1 — Windows input audit

Windows uses native key/mouse mode and assembles terminal-encoded character records defensively. The audit and regression scope are documented in docs/VALIDATION.md. The MSI version is 1.0.1, retaining the permanent UpgradeCode so it upgrades 1.0.0. Run `rudyedit --version` after installation to verify the executable in use. Windows packages and source are delivered for this update; the previously delivered Manjaro binary remains 1.0.0-4. Building the Linux source now produces 1.0.1-1.


## Historical version 1.0.2 — Ctrl+W closes the current panel

Version 1.0.2 assigned Ctrl+W to Close Panel and retained Ctrl+Shift+W as an alternate. That alternate assignment is superseded in 1.0.3. Explicit legacy Close Tab/Ctrl+W mappings are upgraded in memory without rewriting the user's settings. The Windows MSI version increases to 1.0.2 and retains the UpgradeCode and Font-table correction. Windows packages and source are delivered; the earlier Manjaro binary remains unchanged.


## Version 1.0.3 — Ctrl+Shift+W closes the active tab

Ctrl+Shift+W removes only the current tab; Ctrl+W closes the current panel. Both shortcuts retain unsaved-change prompts. Existing XML entries for the former assignments are migrated at startup; the user does not need to delete their configuration. Windows MSI version 1.0.3 retains the permanent UpgradeCode and Font-table correction. Windows installers and source are delivered for this update. The previously delivered Manjaro binary is unchanged; rebuilding Linux from this source produces 1.0.3-1.


Version 2.0.0 — Text mode bars

Formatting bar: Font and Size drop-downs, Bold, Italic, Underline, Text Color, Background Color, Bulleted/Ordered Lists, Spelling, Auto Spell, and Preview. Format opens every control on narrow terminals. Ctrl+Alt+M focuses formatting; arrows select and Enter activates. Font/size remain document markup; terminal cell fonts are host-controlled.

Current-file tags: bottom tag list plus inline Add tags field. Ctrl+Alt+T focuses entry. Enter adds comma-separated tags; Escape returns to editing. Click the tag list to edit/view all tags. Draft entries belong to each tab. Tags are undoable, deduplicated without case, and saved to Tags.xml with Ctrl+S. Ctrl+Shift+T toggles the bar.

File Manager: bottom Tag? field, Ctrl+Alt+G to focus; Enter searches saved Markdown/text files recursively in the displayed folder, matching all comma-separated tags without case. Empty query or × clears results. Search limit: 10,000 paths, 2 MB per file; symlinks/inaccessible files are skipped. All bars are Text-mode only.


Version 2.0.1 — Tag visibility and external storage

Ctrl+Shift+T toggles the current-file tag bar in Text mode in both Code and VIM keybinding modes; hiding it restores two document rows. Ctrl+Alt+T shows the bar and focuses tag entry. View → Toggle Tag Bar and the command palette provide the same action. ShowTagBar = true/false in Config.cs sets startup visibility. Hiding the bar preserves tags and unfinished entries. File Manager tag search remains available when this bar is hidden.

Tag names are stored only in Tags.xml next to Config.cs and KeyboardShortcuts.xml (Windows: %APPDATA%\RudyEdit\Tags.xml; Linux: $XDG_CONFIG_HOME/RudyEdit/Tags.xml or ~/.config/RudyEdit/Tags.xml; RUDYEDIT_CONFIG_HOME overrides the folder). Ctrl+S saves both the file link and its XML tags; unsaved tag changes participate in Save/Discard/Cancel and undo/redo. The configuration file is created on first launch without replacing an existing registry.

Each tagged file stores only an opaque <!-- rudyedit-id: UUID --> comment. RudyEdit removes that metadata from its editing view and reinserts it on save, so typing, selecting, copying, and previewing the document do not expose the code. Markdown renderers hide the comment; raw text tools can display it. Moving/renaming files retains their tags; Save As assigns an independent identifier. Removing all tags leaves an empty registry entry. Search resolves identifiers through Tags.xml and still finds legacy tag headers pending migration.

Legacy leading <!-- tags: ... --> headers are staged for migration when opened: the editor displays the document body and its tags, and the next save moves names to XML and replaces the old header with the identifier. No unopened documents are rewritten. Back up Tags.xml with the documents; a file identifier by itself cannot reconstruct tag names on another machine. Corrupt XML and conflicting tag edits cause a save error instead of overwriting the registry. The registry uses <Tags Version="1"><File Id="UUID" Path="..."><Tag>name</Tag></File></Tags>; UUID is a real GUID and tag text is XML escaped. Tags.xml is validated before saving from RudyEdit.


## Version 2.0.2 — Binder and fountain-pen icon

The SVG master in assets/rudyedit.svg supplies the binder, three silver rings, outlined ornamental R, and gold-nib fountain pen. assets/rudyedit.ico includes 16/24/32/48/64/128/256 px images and is embedded through ApplicationIcon in the C# project. Windows Setup uses the same ICO via MUI_ICON. The MSI Icon table supplies both shortcuts and ARPPRODUCTICON; its validator compares the embedded icon stream with the source ICO. The permanent UpgradeCode remains unchanged.

The Manjaro package installs the SVG in usr/share/icons/hicolor/scalable/apps/rudyedit.svg and a transparent PNG in usr/share/icons/hicolor/256x256/apps/rudyedit.png. The existing desktop entry selects Icon=rudyedit. Both release binaries bundle .NET 10.0.13.

Icon regeneration is optional for normal builds: binary derivatives are included in source. To regenerate, install Inkscape and Python Pillow and run python3 tools/build_icons.py. tools/verify_icons.py uses only the Python standard library to compare every icon frame embedded in Windows PE files against assets/rudyedit.ico.


Version 2.0.3 — Error logging, configuration folder, and one Windows installer

Ctrl+Alt+C (Open Config Folder in Help) shows the configuration directory in RudyEdit File Manager and requests Explorer on Windows or the desktop file manager through xdg-open on Linux. It works in Code/VIM and Text/Development modes; the in-app folder remains usable when no desktop opener is available. Existing user keybinding files receive the shortcut through the default catalog unless the global sequence has been customized. Both distributed XML keybinding files include it explicitly.

RudyEdit-errors.log is created beside Config.cs, KeyboardShortcuts.xml, Tags.xml and languages.json. It records UTC timestamps, process IDs, context, exceptions/stacks and nested errors, startup/configuration/save/command failures, tool stderr and nonzero exits, compiler/LSP diagnostics, and debugger/protocol/background failures. Help → Error Log displays a searchable snapshot. The log rotates at 5 MiB with three backups; very long entries are marked/truncated at 65,536 characters. If the config folder is unwritable, the active log falls back to RudyEdit-<user>/RudyEdit-errors.log under the system temp directory. Fatal errors report the active path. Normal cancellations and cleanup races are excluded.

Windows users install with the single self-contained RudyEdit-2.0.4-Windows-Setup.exe. No separate MSI or .NET runtime download is needed. It preserves the binder-and-pen icon, optional Desktop shortcut, Start Menu entry, PATH update, bundled runtime/font, and existing upgrade identity. Windows Installer's verbose installation log remains at %TEMP%\RudyEdit-install.log. The internal MSI is a build intermediate, not an additional user installer.

## Version 2.0.4 — Nerd Font UI

The installed FiraCode Nerd Font Mono now supplies 78 menu, formatting and File Manager glyphs. `tools/verify_nerd_font.py` uses Python's standard library to validate every catalog codepoint against the shipped TTF Unicode cmap and horizontal metrics. Run it alongside the C# suite before packaging. The font remains unmodified and both font licenses remain included. Setup's finish page explains that the user must select the terminal profile font; installation does not edit terminal preferences. New assets/font-icons-help.md ships in both publishes. UseNerdIcons defaults to true without rewriting existing user Config.cs. The public Windows release remains one Setup EXE; no separate MSI download.

## Mandatory executable tests — 2.0.5 and every later revision

Before the packaging commands above will work, build the test harness, publish the selected target, stage its diagnostics and run the executable on its native OS:

```sh
dotnet build tests/RudyEdit.Tests -c Release
# Publish as described above; use the target host for the test invocation.
python3 tools/stage_release_tests.py dist/linux-x64
python3 tests/release_runner.py --executable dist/linux-x64/rudyedit
python3 tests/release_gate.py dist/linux-x64
python3 packaging/manjaro/package.py
```

Windows uses `dist/win-x64/rudyedit.exe`. Run the test script on Windows with Python 3 before invoking the MSI builder or NSIS. `tools/build.sh` defaults to Linux and stops before packaging if any test fails/blocks. `tools/build.ps1` defaults to Windows and also stops before packaging commands if tests fail/block. Both can publish a foreign target, but that target must then be tested on a native host. The runner writes `tests/results/release-tests.log` and `.json` inside the publish directory by default. For installed apps, select a writable report path or use the packaged convenience scripts.

MSI/pacman builders and NSIS verify the native report again before writing output. NSIS uses python3 by default; on Windows use `/DPYTHON=python` when Python is registered as `python`. No skip-test switch is provided. A payload hash includes the executable, runtime, assets, test DLL and scripts; changing any file invalidates the result. Reports and Python caches are excluded. You may copy a fully tested Windows publish tree including its receipt to a Linux packaging host; the receipt is checked against its bytes without pretending the Windows EXE ran on Linux.

Package the same tested tree. Its scripts and completed reports are installed with the app. The managed runner is copied after publication by `tools/stage_release_tests.py`; no test framework or SDK is needed on the end-user machine. Python and the Linux PTY test-only dependencies are still required to rerun diagnostics. A source ZIP includes test sources and build tooling.

For this revision, native Windows execution is unavailable on the Linux build host. A blocked Windows report and Windows validation ZIP are provided instead of creating an installer without the required pass. Build the installer only after running the validation ZIP's tests successfully on Windows and returning/copying the passing publish-tree receipt.
