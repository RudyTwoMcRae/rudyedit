# Specification coverage — 2.0.5

| Requested feature | Implemented behaviour and scope |
|---|---|
| Executable release tests | Packaged scripts and runner; all command IDs verified in Code/VIM; stats/errors/test-list reports; native-platform and payload-hash gates before all installers |
| C# console app, Windows and Manjaro | .NET 10 C# source; self-contained x64 builds for each OS |
| Install, update, uninstall | Windows MSI plus setup wrapper; stable MSI UpgradeCode; pacman package for Manjaro; native uninstall registrations |
| Graphic UI and file icons | 78 verified single-cell Nerd Font glyphs; menu/command/formatting icons; file-type and special-folder icons, colored with visible names; UseNerdIcons plain-label fallback; font setup/legend in Help |
| Fonts and menu entries | FiraCode Nerd Font Mono bundled and installed; Windows Start Menu, optional Desktop; Linux application menu |
| CLI -f and -p | Implemented, including paths with spaces and both flags together |
| Text/Development modes | F3 or Mode menu; Build menu appears in Development mode |
| Code/VIM keybindings | F2; familiar Code editing and documented Vim subset, not complete emulation |
| Bold/italic/underline | Markdown emphasis and HTML underline wrappers; selection and future typing |
| Font family, size, text colour | Stored as HTML span styles; normal terminal cells retain host-controlled font/size |
| Background colour | Session editor colour picker; persistent default in Config.cs |
| Bullet and ordered lists | Prefix selected lines; Enter continues a recognized list |
| Markdown compatibility | Portable Markdown source and optional HTML; Joplin/other reader sanitization may affect styles |
| Tags | Tags.xml registry with hidden file identifiers; Ctrl+Shift+T toggles the tag bar; edit/add tags and search tagged documents |
| Spell checker/as-you-type | English word list with suggestions, underlines, personal dictionary; not a full Hunspell morphology engine |
| Tabs and panels | Per-pane tabs, recursive vertical/horizontal splits, navigation, Ctrl+Shift+W removes the current tab, Ctrl+W closes the current panel, close tab/panel others commands; unsaved-change prompts; up to eight panes |
| Mouse | Click menus, commands, tabs and panels; click to position the cursor; drag or Shift-click to select; wheel scrolling; popup close button. SGR terminal input on Linux and native console mouse events on Windows |
| Syntax highlighting | Lightweight lexical highlighter for all requested host-language families; not a full parser |
| Autocompletion | Explicit Ctrl+Space local keywords/file symbols; optional LSP for semantic/project completion |
| Tailwind / CodeIgniter / Next / Nuxt | Host-language highlighting, common identifiers, related offline guides; external project tools via console |
| Build/run C# | Nearest csproj via dotnet; SDK required |
| Build/run C | gcc single-source default; larger builds via console/Makefile; compiler required |
| Build/run Python | py_compile syntax check and Python script execution; Python required |
| Build/run Rust | Nearest Cargo.toml via cargo; toolchain required |
| Console | Command runner with captured output and line stdin; separate shell per command, not a full PTY terminal |
| Error checking | Build diagnostics and optional live LSP diagnostics; jump to selected error |
| Language detection | Extension-based, including JSX/TSX, Vue and Prisma |
| Debug line stepping | Python bdb included; C/Rust gdb MI and C# netcoredbg DAP integrations; external adapters required as documented |
| Debug controls | Breakpoint, continue, next/into/out, locals, stop; gdb breakpoint updates need restart; no attach/multi-thread UI |
| Offline language help | Original practical guides for every requested language/framework, searchable in a popup; not full upstream manuals |
| Config.cs | Supplied declarative syntax, validated values, mode-dependent ShowConsole, in-editor editing and F1 guide |
| Shortcut XML | All supplied contexts and chords, added defaults, user command remapping, reload-on-save and F1 catalog |
| Main menu | Supplied File/Mode/Text/Build/Help actions, plus Edit/View and additional requested editor operations |

Supporting documents are in specification/; their shortcut and menu definitions include the current Ctrl+Shift+W tab-close and Ctrl+W panel-close assignments. Runtime configuration lives in assets/ and is copied to each user's configuration directory on first launch.


Version 2.0.3 — Text mode bars

Formatting bar: Font and Size drop-downs, Bold, Italic, Underline, Text Color, Background Color, Bulleted/Ordered Lists, Spelling, Auto Spell, and Preview. Format opens every control on narrow terminals. Ctrl+Alt+M focuses formatting; arrows select and Enter activates. Font/size remain document markup; terminal cell fonts are host-controlled.

Current-file tags: bottom tag list plus inline Add tags field. Ctrl+Alt+T focuses entry. Enter adds comma-separated tags; Escape returns to editing. Click the tag list to edit/view all tags. Draft entries belong to each tab. Tags are undoable, deduplicated without case, and saved to Tags.xml with Ctrl+S. Ctrl+Shift+T toggles the bar.

File Manager: bottom Tag? field, Ctrl+Alt+G to focus; Enter searches saved Markdown/text files recursively in the displayed folder, matching all comma-separated tags without case. Empty query or × clears results. Search limit: 10,000 paths, 2 MB per file; symlinks/inaccessible files are skipped. All bars are Text-mode only.


Version 2.0.3 — Tag visibility and external storage

Ctrl+Shift+T toggles the current-file tag bar in Text mode in both Code and VIM keybinding modes; hiding it restores two document rows. Ctrl+Alt+T shows the bar and focuses tag entry. View → Toggle Tag Bar and the command palette provide the same action. ShowTagBar = true/false in Config.cs sets startup visibility. Hiding the bar preserves tags and unfinished entries. File Manager tag search remains available when this bar is hidden.

Tag names are stored only in Tags.xml next to Config.cs and KeyboardShortcuts.xml (Windows: %APPDATA%\RudyEdit\Tags.xml; Linux: $XDG_CONFIG_HOME/RudyEdit/Tags.xml or ~/.config/RudyEdit/Tags.xml; RUDYEDIT_CONFIG_HOME overrides the folder). Ctrl+S saves both the file link and its XML tags; unsaved tag changes participate in Save/Discard/Cancel and undo/redo. The configuration file is created on first launch without replacing an existing registry.

Each tagged file stores only an opaque <!-- rudyedit-id: UUID --> comment. RudyEdit removes that metadata from its editing view and reinserts it on save, so typing, selecting, copying, and previewing the document do not expose the code. Markdown renderers hide the comment; raw text tools can display it. Moving/renaming files retains their tags; Save As assigns an independent identifier. Removing all tags leaves an empty registry entry. Search resolves identifiers through Tags.xml and still finds legacy tag headers pending migration.

Legacy leading <!-- tags: ... --> headers are staged for migration when opened: the editor displays the document body and its tags, and the next save moves names to XML and replaces the old header with the identifier. No unopened documents are rewritten. Back up Tags.xml with the documents; a file identifier by itself cannot reconstruct tag names on another machine. Corrupt XML and conflicting tag edits cause a save error instead of overwriting the registry. The registry uses <Tags Version="1"><File Id="UUID" Path="..."><Tag>name</Tag></File></Tags>; UUID is a real GUID and tag text is XML escaped. Tags.xml is validated before saving from RudyEdit.


Version 2.0.3 — Application icon

A three-ring binder with an ornamental gold R and vintage fountain pen is the application icon. The editable SVG, transparent PNG, and Windows ICO are included. Windows executable, Setup, Installed Apps, and Start Menu/optional Desktop shortcut icons use it; the Manjaro application-menu entry uses the installed SVG/PNG hicolor icons. Terminal-host window and tab icon behavior is host-controlled.


Version 2.0.3 — Error logging, configuration folder, and one Windows installer

Ctrl+Alt+C (Open Config Folder in Help) shows the configuration directory in RudyEdit File Manager and requests Explorer on Windows or the desktop file manager through xdg-open on Linux. It works in Code/VIM and Text/Development modes; the in-app folder remains usable when no desktop opener is available. Existing user keybinding files receive the shortcut through the default catalog unless the global sequence has been customized. Both distributed XML keybinding files include it explicitly.

RudyEdit-errors.log is created beside Config.cs, KeyboardShortcuts.xml, Tags.xml and languages.json. It records UTC timestamps, process IDs, context, exceptions/stacks and nested errors, startup/configuration/save/command failures, tool stderr and nonzero exits, compiler/LSP diagnostics, and debugger/protocol/background failures. Help → Error Log displays a searchable snapshot. The log rotates at 5 MiB with three backups; very long entries are marked/truncated at 65,536 characters. If the config folder is unwritable, the active log falls back to RudyEdit-<user>/RudyEdit-errors.log under the system temp directory. Fatal errors report the active path. Normal cancellations and cleanup races are excluded.

Windows users install with the single self-contained RudyEdit-2.0.3-Windows-Setup.exe. No separate MSI or .NET runtime download is needed. It preserves the binder-and-pen icon, optional Desktop shortcut, Start Menu entry, PATH update, bundled runtime/font, and existing upgrade identity. Windows Installer's verbose installation log remains at %TEMP%\RudyEdit-install.log. The internal MSI is a build intermediate, not an additional user installer.

Version 2.0.4 — Graphic icons

Both installers include **FiraCode Nerd Font Mono**. Select that font in your terminal host so its Nerd Font glyphs render correctly. In Windows Terminal, open Settings → your profile → Appearance → Font face and choose **FiraCode Nerd Font Mono**. For Ghostty, set:

```ini
font-family = "FiraCode Nerd Font Mono"
```

Reload Ghostty's configuration; restart a terminal if the newly installed font is not listed. [Windows Terminal font setup](https://learn.microsoft.com/windows/terminal/tutorials/custom-prompt-setup) · [Ghostty configuration](https://ghostty.org/docs/config). Portable users must install `assets/fonts/FiraCodeNerdFontMono-Regular.ttf` themselves. Text → Font sets document markup; the terminal host controls the screen's font.

Main menus, command lists, the command palette and Text-mode formatting controls now have graphic icons. The File Manager shows colored, distinct icons for languages, text/Markdown, data/configuration, images, archives, office documents, PDF, audio/video, fonts, and binaries, plus parent/open/ordinary folders and Git, GitHub, source, configuration and dependency folders. Tag-search results use the same icons. File names and extensions stay visible; icons do not add support for opening binary formats.

**Help → Nerd Font & Icons** provides setup instructions and a complete icon legend. If the terminal cannot display the glyphs, **View → Toggle Graphic Icons** switches to plain file-type labels for this session. Set `UseNerdIcons = false;` in Config.cs for a persistent fallback; the default is `true`, including when older configuration files omit this setting. Existing configuration and keybindings are preserved. All keyboard/menu/mouse controls still work with either display style.
