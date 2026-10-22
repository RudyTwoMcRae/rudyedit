# RudyEdit 2.0.5

A C#/.NET terminal editor for Windows x64 and Manjaro Linux x86_64, built from the four supplied specification files in `specification/`.

RudyEdit provides version 2.0 of the requested editor. It has Text and Development modes, a Code-style keymap and a practical Vim subset, Markdown formatting, tabs and recursive split panes, a file browser, a command console, local completion, optional language-server integration, and source-level debugger integrations. The feature matrix in `docs/FEATURES.md` identifies the implemented behaviour and its limits. It is not yet a complete replacement for all VS Code or Neovim features.

## Install

**Windows:** Release installers are created only after the native Windows executable passes the required tests. Run the approved `RudyEdit-<version>-Windows-Setup.exe`. The installer offers an optional Desktop shortcut and installs the app, its .NET runtime, FiraCode Nerd Font Mono, and a Start Menu entry. This is the single, self-contained Windows installer; no separate MSI or runtime download is required. Restart your terminal after installation to pick up PATH changes. Uninstall through Windows Settings → Apps → Installed apps. Future versions use the same MSI UpgradeCode; a new installer upgrades the installed version. Existing per-user settings and documents are preserved.

**Manjaro:** Open a terminal in the download folder:

```sh
sudo pacman -U ./rudyedit-2.0.5-1-x86_64.pkg.tar.zst
```

Dependencies are declared in the package. Install or upgrade using `pacman -U`; remove using `sudo pacman -R rudyedit`. The package includes its runtime, font, launcher, and application-menu entry. Select **FiraCode Nerd Font Mono** in your terminal's preferences for the intended glyphs. Use View → Toggle Graphic Icons for plain labels with ordinary monospace fonts.

**Portable use from source:** After publishing, run `dist/win-x64/rudyedit.exe` on Windows or `dist/linux-x64/rudyedit` on Linux. Keep all files in that publish directory together. This does not modify PATH, register fonts, or create menu entries.


## Graphic icons and terminal font

Both installers include **FiraCode Nerd Font Mono**. Select that font in your terminal host so its Nerd Font glyphs render correctly. In Windows Terminal, open Settings → your profile → Appearance → Font face and choose **FiraCode Nerd Font Mono**. For Ghostty, set:

```ini
font-family = "FiraCode Nerd Font Mono"
```

Reload Ghostty's configuration; restart a terminal if the newly installed font is not listed. [Windows Terminal font setup](https://learn.microsoft.com/windows/terminal/tutorials/custom-prompt-setup) · [Ghostty configuration](https://ghostty.org/docs/config). Portable users must install `assets/fonts/FiraCodeNerdFontMono-Regular.ttf` themselves. Text → Font sets document markup; the terminal host controls the screen's font.

Main menus, command lists, the command palette and Text-mode formatting controls now have graphic icons. The File Manager shows colored, distinct icons for languages, text/Markdown, data/configuration, images, archives, office documents, PDF, audio/video, fonts, and binaries, plus parent/open/ordinary folders and Git, GitHub, source, configuration and dependency folders. Tag-search results use the same icons. File names and extensions stay visible; icons do not add support for opening binary formats.

**Help → Nerd Font & Icons** provides setup instructions and a complete icon legend. If the terminal cannot display the glyphs, **View → Toggle Graphic Icons** switches to plain file-type labels for this session. Set `UseNerdIcons = false;` in Config.cs for a persistent fallback; the default is `true`, including when older configuration files omit this setting. Existing configuration and keybindings are preserved. All keyboard/menu/mouse controls still work with either display style.

## Start

```sh
rudyedit
rudyedit -f "path/to/notes.md"
rudyedit -p "path/to/project"
rudyedit "path/to/file.py"
rudyedit --doctor
```

`-f` opens the file; a nonexistent filename begins a new file. `-p` sets the working folder and opens its file browser. Both flags can be combined.

## First session

- **Alt+F/E/M/T/V/B/H** opens File/Edit/Mode/Text/View/Build/Help directly in either keybinding mode. Build appears in Development mode. **F10** opens the menu; during debugging it steps over, and the Alt shortcuts still open menus.
- Menu initials are underlined. Click a main menu and then a command to activate it; click **×** to close a popup.
- Click in a document to place the cursor, drag to select text, or Shift-click to extend a selection. The wheel scrolls the document under the pointer. Click a tab or another panel to focus it. Mouse selection works in both Code and VIM modes.
- **Arrow keys** move the cursor in both Code and VIM modes, including VIM Normal, Insert, and Visual modes.
- **Ctrl+Shift+W** (Shift+Ctrl+W) closes and removes the current tab, prompting for unsaved changes and selecting a remaining tab. The panel stays open; closing its last tab leaves a blank document. **Ctrl+W** closes the current panel, checking its unsaved tabs and rearranging the remaining panels. Closing the last panel leaves a blank editor; **Ctrl+Q** exits.
- **F1** opens contextual help. **Shift+F1** lists shortcuts. **Ctrl+F1** opens the current language's offline guide.
- **F2** toggles Code/VIM keys. **F3** toggles Text/Development mode.
- **Ctrl+S** saves; **Ctrl+O** opens; **Ctrl+N** creates a tab; **Ctrl+Q** exits with unsaved-change prompts in either keybinding mode, including from dialogs, pending shortcuts, the file manager, and the console. Save, Discard, or Cancel remains available; repeated Ctrl+Q never bypasses the prompt. Exiting restores the terminal.
- **Shift+arrows** selects; **Ctrl+C/X/V** copies/cuts/pastes within RudyEdit. Your terminal's own copy/paste shortcuts access the system clipboard.
- **Shift+Space** toggles the file browser. **F8** shows/focuses it as a fallback. **Esc** returns to the editor.
- **Ctrl+Alt+V/H** splits vertically/horizontally. **Alt+Left/Right** changes panes. **Ctrl+Alt+Left/Right** changes tabs.
- The supplied chords also work: **Ctrl+K, P, Right** (next pane), **Ctrl+K, F** (find), **Ctrl+K, Shift+F** (replace), **Ctrl+K, C** (console). Complete a chord within 1.5 seconds. The default XML uses **Ctrl+Alt+Left/Right Arrow** for tabs; existing user XML is preserved, including any older tab chords.

In the file browser: Enter opens; `v`/`h` opens a split; `t` opens a tab; `n` creates a file; `f` creates a folder; `y` copies; `d` cuts; `p` pastes; `l` loads a folder; Ctrl+F filters names in the current folder. Copy/move refuses to overwrite existing destinations. Directory symlinks are skipped during recursive copying.

## Text mode

The formatting bar provides font and size drop-downs, Bold, Italic, Underline, text/background colours, lists, spelling, and preview. **Format ▾** exposes every control on narrower terminals. Click a control, or press **Ctrl+Alt+M**, use Left/Right, and press Enter. Escape returns to the document.

The bottom bar shows the current file's tags. Click the tag list to view/edit the complete list when it exceeds the screen width. Click **Add tags** or press **Ctrl+Alt+T**, type comma-separated tags, and press **Enter** to add them. **Escape** returns to the document. Each tab keeps its own unfinished entry. Tags are case-insensitively deduplicated; adding tags is undoable and preserves the document selection. **Ctrl+Shift+T** toggles this bar; **Ctrl+Alt+T** shows it and focuses entry. Set `ShowTagBar = false;` in Config.cs to hide it at startup. **Ctrl+S** saves the file identifier and updates `Tags.xml`.

The File Manager's bottom **Tag?** field searches saved `.md`, `.markdown`, and `.txt` files recursively beneath its displayed folder. Click the field or press **Ctrl+Alt+G**, enter comma-separated tags, and press Enter. Results must contain **all** entered tags; matching ignores case. Enter opens a selected result. Click **×** or submit an empty search to restore the ordinary file list. Searches inspect at most 10,000 paths, skip symlinks/inaccessible files, and skip files larger than 2 MB. Save changed tags before searching. These bars appear only in Text mode and reserve space without covering document text.

Text files use Markdown source, compatible with common Markdown readers. Bold and italic use Markdown markers. Underline, font family, point size, and text colour use HTML markup; support varies by reader. Font family and point size cannot be applied to individual cells in a normal console. Choose the terminal's font and size in the terminal host. Text → Markdown Preview shows a basic styled terminal view, not a complete HTML renderer. Format existing selections or insert paired markers and type between them.

Tag names live only in `Tags.xml` beside Config.cs. Saved tagged documents contain an opaque `<!-- rudyedit-id: UUID -->` identifier, hidden in RudyEdit's editor and Markdown preview. Raw file viewers can show the identifier comment. Moving or renaming a file preserves its association. Save As creates an independent association. Existing legacy tag headers are removed from the editing view on open and migrate to XML on the next save; unchanged files on disk stay as they are until saved. Back up Tags.xml with tagged documents, and copy it to the destination configuration folder when moving between computers. Search Tags focuses the File Manager tag-search field in Text mode. Spelling uses a bundled English word list, with Hunspell/plain-word dictionaries if available. F7 shows suggestions and can add a word to a personal dictionary. This is a word-list checker, not a grammar checker.

## Development mode

Supported source families: C#, C, HTML, CSS, JavaScript/TypeScript, PHP, React, Vue, Python, Prisma, Rust, and XML. Tailwind, CodeIgniter, Next.js, and Nuxt are covered through their host-language files and related offline guides. Syntax colouring is lexical. Local completion combines keywords and file identifiers; **Ctrl+Space** invokes it.

For semantic completion and live diagnostics, install the relevant language server and use Build → Connect Language Server. Edit `languages.json` in the configuration folder for its executable/arguments. Servers are optional and are not bundled. The client implements full-document synchronization, completion text edits, and diagnostics; it is not a complete LSP client.

**F6** builds. **Ctrl+F5** builds/runs C#, C, Python, or Rust using installed toolchains. C's default build is for a single source file; use a Makefile in the console for larger projects. C# uses the nearest `.csproj`; Rust uses the nearest `Cargo.toml`. Python's build checks syntax with `py_compile`. Use the console for npm/Composer/framework build commands.

**Ctrl+`** focuses the command console. Enter starts a shell command or sends a line of input to the running program; Esc returns to the editor. **Shift+F5** stops it. Output is captured in a scrollable pane. This console uses redirected streams; it is not a PTY emulator for interactive TUIs, full shell sessions, or password prompts. Each command starts in the current working folder; use Open Folder to persistently change it.

### Debugging

Save the source first. Set a breakpoint with **F9**. **F5** starts/continues; **F10** steps over; **F11** steps into; **Shift+F11** steps out. Build → Variables inspects locals while paused. **Shift+F5** stops debugging.

- **Python:** install Python 3 on PATH. The bundled stdlib `bdb` adapter pauses at the first script line. Console input supports the built-in `input()` function.
- **C / Rust:** install `gdb`, build a debug executable with F6, then select it in F5. Changed breakpoints require restarting the gdb session. Compile with debug symbols and low optimization. Interactive target stdin is not supported in this debugger integration; use Run for those programs.
- **C#:** install [netcoredbg](https://github.com/Samsung/netcoredbg), build Debug with F6, then select the application DLL next to its PDB in F5. Put netcoredbg on PATH or set `RUDYEDIT_NETCOREDBG` to its executable. A compatible .NET runtime for the target project is also required by the debugger. C# interactive target stdin is not supported in this integration.

Toolchains/debuggers/language servers are separate developer dependencies. The installer bundles the runtime needed to run RudyEdit, not the .NET SDK, Python, GCC, Cargo, gdb, or netcoredbg. Run `rudyedit --doctor` for availability. See `docs/TOOLCHAINS.md`.

## Configuration

User settings are created on first launch:

- Windows: `%APPDATA%\RudyEdit\`
- Linux: `$XDG_CONFIG_HOME/RudyEdit/` or `~/.config/RudyEdit/`
- Override with an absolute `RUDYEDIT_CONFIG_HOME` path.

**Ctrl+Alt+C** opens the configuration folder in both keybinding modes and both application modes. Help → Open Config Folder does the same. The folder is immediately available in RudyEdit File Manager; Explorer opens on Windows and xdg-open requests the desktop file manager on Linux. A missing desktop opener is logged and the in-app folder stays usable.

**Ctrl+F2** edits Config.cs. This is a validated declarative settings format shaped like the supplied file, not arbitrary C# execution. Descriptive/allowed-value annotations from the specification have been removed. **Shift+F2** edits KeyboardShortcuts.xml; your original bindings are included. Saving reloads valid settings/maps, and an invalid edit preserves the last working settings for the current session. F1 in either file explains valid values/commands with examples. New shortcuts must reference a command from the catalog; XML cannot introduce executable code.

## Error logging

RudyEdit creates `RudyEdit-errors.log` beside Config.cs. It records UTC timestamps, process IDs, error context, exception details and stacks, configuration errors, save/command errors, compiler and language-server diagnostics, tool/debugger stderr, nonzero tool exits, and unexpected protocol/background failures. Help → Error Log shows a searchable snapshot of the active log.

The current log rotates at 5 MiB and keeps three backups (`.1`–`.3`). Entries exceeding 65,536 characters are marked and truncated. If the configuration folder is unwritable, logging falls back to `RudyEdit-<user>/RudyEdit-errors.log` under the system temporary directory; Help → Error Log and fatal-error output show the active path. If both locations fail, the error still appears in the app and the status reports that logging is unavailable. Normal user cancellations and expected process-exit races are not errors.

Windows installation uses a separate verbose log at `%TEMP%\RudyEdit-install.log`.

## Compatibility and limits

A modern UTF-8 terminal with ANSI true colour is recommended, at least 80×24. The app redraws changed rows through an offscreen buffer without clearing on each keypress. Windows Console VT output is enabled at startup. Flicker and font rendering still depend on the host terminal and cannot be guaranteed on every machine.

Legacy terminal encodings cannot distinguish certain keys: Shift+Space may equal Space, Ctrl+I may equal Tab, Ctrl+H may equal Backspace, and Ctrl+M may equal Enter. RudyEdit requests enhanced key reporting on supporting Linux terminals. Menus and the command palette provide fallbacks. Windows Terminal and Ghostty bindings can also reserve shortcuts for themselves.

UTF-8 text up to 8 MiB is supported. UTF-8 BOM, CRLF/LF, and executable permissions on existing Unix files are preserved. Binary/UTF-16 files are rejected. Saves use an adjacent temporary file and atomic replacement with a hash check against external edits. Symlinks, ACLs, and extended metadata are not a full fidelity round trip: saving replaces the path with a regular file. There is a small check-to-replace window with concurrent external writers. Keep backups for important work. Undo history is memory-based, and tabs/panes are session-only. Full Vim compatibility, multi-cursor editing, plugin hosting, semantic refactoring, and full IDE project/debug controls are outside this release.


## Application icon

The application uses a vector three-ring binder with a gold calligraphic R and a vintage fountain pen. Windows embeds a seven-size ICO (16, 24, 32, 48, 64, 128, and 256 px) in rudyedit.exe, the Setup executable, Start Menu/Desktop shortcuts, and Installed Apps. Manjaro installs the SVG and a transparent 256 px PNG under the hicolor application-icon theme. Terminal windows and tabs may use the terminal host's icon.

The editable master is `assets/rudyedit.svg`. The generated `assets/rudyedit.ico` and `assets/rudyedit.png` are checked in, so normal builds require no graphics tools. To regenerate them after editing the SVG, install Inkscape and Python Pillow, then run `python3 tools/build_icons.py`. After publishing Windows, run `python3 tools/verify_icons.py dist/win-x64/rudyedit.exe` to check the embedded icon resources.

## Build from source

Install the .NET 10 SDK, then:

```sh
dotnet build tests/RudyEdit.Tests -c Release
dotnet run --project tests/RudyEdit.Tests -c Release --no-build
dotnet run --project src/RudyEdit -- -p .
```

`tools/build.sh` publishes both x64 platforms with a bundled .NET 10.0.13 runtime and packages the installers when Python 3, zstd, wixl, and NSIS are present. `tools/build.ps1` publishes from Windows. See `packaging/README.md` for installer tooling, and `docs/VALIDATION.md` for the actual checks performed for this release.


Version 1.0.3: Ctrl+Shift+W removes the current tab; Ctrl+W closes the current panel. Both commands check unsaved changes. Default and specification keybinding files match these assignments. Older Close Panel/Ctrl+Shift+W entries are updated to Close Tab on startup, preserving unrelated custom bindings. In-app Help, the File/View menus, and the shortcut list show the new mapping.


Version 2.0.1 — Tag visibility and external storage

Ctrl+Shift+T toggles the current-file tag bar in Text mode in both Code and VIM keybinding modes; hiding it restores two document rows. Ctrl+Alt+T shows the bar and focuses tag entry. View → Toggle Tag Bar and the command palette provide the same action. ShowTagBar = true/false in Config.cs sets startup visibility. Hiding the bar preserves tags and unfinished entries. File Manager tag search remains available when this bar is hidden.

Tag names are stored only in Tags.xml next to Config.cs and KeyboardShortcuts.xml (Windows: %APPDATA%\RudyEdit\Tags.xml; Linux: $XDG_CONFIG_HOME/RudyEdit/Tags.xml or ~/.config/RudyEdit/Tags.xml; RUDYEDIT_CONFIG_HOME overrides the folder). Ctrl+S saves both the file link and its XML tags; unsaved tag changes participate in Save/Discard/Cancel and undo/redo. The configuration file is created on first launch without replacing an existing registry.

Each tagged file stores only an opaque <!-- rudyedit-id: UUID --> comment. RudyEdit removes that metadata from its editing view and reinserts it on save, so typing, selecting, copying, and previewing the document do not expose the code. Markdown renderers hide the comment; raw text tools can display it. Moving/renaming files retains their tags; Save As assigns an independent identifier. Removing all tags leaves an empty registry entry. Search resolves identifiers through Tags.xml and still finds legacy tag headers pending migration.

Legacy leading <!-- tags: ... --> headers are staged for migration when opened: the editor displays the document body and its tags, and the next save moves names to XML and replaces the old header with the identifier. No unopened documents are rewritten. Back up Tags.xml with the documents; a file identifier by itself cannot reconstruct tag names on another machine. Corrupt XML and conflicting tag edits cause a save error instead of overwriting the registry. The registry uses <Tags Version="1"><File Id="UUID" Path="..."><Tag>name</Tag></File></Tags>; UUID is a real GUID and tag text is XML escaped. Tags.xml is validated before saving from RudyEdit.

## Packaged executable tests — version 2.0.5

The application includes `tests/release_runner.py`, a managed test DLL launched by `rudyedit --self-test`, protocol fixtures, terminal scripts, gate regression tests, font/icon audits, and `tests/README.md`. `tests/Run-Tests.cmd` on Windows and `tests/Run-Tests.sh` on Linux write a report to the current user's configuration folder. Python 3 is required for these diagnostics; Linux terminal tests additionally use `tests/requirements.txt`. A test run uses temporary settings/documents and does not overwrite your work.

The report starts with performed/passed/error/blocked totals, then all errors, then all named tests/checkpoints. Its JSON companion records the executable's command catalog, coverage, platform, UTC times and payload SHA-256. All 83 current commands have outcome tests in both Code and VIM modes. The gate fails when a newly added command has no passing test. Existing document, keyboard, mouse, tags, configuration, logging, font, process and protocol regressions remain part of the suite.

Every revision builds, publishes, stages diagnostics and runs native executable
tests. Windows Setup is then built regardless of failed/blocked tests, as requested
October 22, 2026. Completed native attempts and unchanged payload hashes are still
required. Full failure reports ship with Setup and remain attached to the release;
failed tests retain a failing workflow status. Linux/pacman still requires a full
pass. See tests/README.md and packaging/README.md for policy and scope.
