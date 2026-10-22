# Config.cs reference

This is a safe settings DSL, not executable C#. Values are validated and never evaluated as code. Use one assignment per line and semicolons. F1 opens this guide from Config.cs; Ctrl+S reloads valid values. Invalid files leave the current session's last valid settings active; a new launch falls back to defaults and displays the error.

UseNerdIcons — true or false. Display Nerd Font glyphs on menus, formatting controls and file/folder entries. The terminal must use FiraCode Nerd Font Mono. False uses plain file-type labels. View → Toggle Graphic Icons changes the session; Help → Nerd Font & Icons gives setup instructions and a legend. Older files that omit this setting default to true.
Example: UseNerdIcons = true;

LineNumbers — true or false. Show editor line numbers.
Example: LineNumbers = true;

ShowTagBar — true or false. Show the current-file tag bar at startup in Text mode. Ctrl+Shift+T toggles it during a session; Ctrl+Alt+T shows it and focuses entry.
Example: ShowTagBar = true;

OpenFileManager — true or false. Show the file browser at startup; -p also opens it.
Example: OpenFileManager = true;

DefaultKeysMode — Code or VIM. Choose the initial keybinding style. F2 changes it during a session.
Example: DefaultKeysMode = VIM;

DefaultMode — Text or Development. Initial application mode. F3 toggles it.
Example: DefaultMode = Development;

DefaultTab — integer 1–16. Display width of a literal tab, or number of inserted spaces when TabSpaces is true.
Example: DefaultTab = 4;

TabSpaces — true or false. Insert spaces rather than an actual tab.
Example: TabSpaces = false;

FileManagerWidth — integer 10–50. Desired browser width as a percentage of terminal width. Minimum usable widths can override this on small screens.
Example: FileManagerWidth = 25;

FontSize — integer 6–96. Default point size offered by Text → Font Size. Stored as an HTML span in Markdown; the terminal's font size is set in the host terminal.
Example: FontSize = 12;

ConsoleHeight — integer 10–70. Desired output area as percentage of terminal height. The UI clamps this to usable dimensions.
Example: ConsoleHeight = 30;

ShowConsole — true or false. Default console visibility for each application mode. This is evaluated only inside the supported Mode condition, rather than arbitrary C#.
Example:
if (Mode == Development)
{
    ShowConsole = true;
}
else
{
    ShowConsole = false;
}

SpellCheck — true or false. Underline unknown English words in Text mode.
Example: SpellCheck = true;

Font — quoted font family. The preferred document font label; the installed terminal font is selected in the host terminal's settings. Select the installed FiraCode Nerd Font Mono family in your terminal preferences.
Example: Font = "FiraCode Nerd Font Mono";

Foreground — a quoted #RRGGBB colour. Default editor foreground.
Example: Foreground = "#d8dee9";

Background — a quoted #RRGGBB colour. Default editor background.
Example: Background = "#151c28";

Paths:
Windows: %APPDATA%\RudyEdit\Config.cs
Linux: $XDG_CONFIG_HOME/RudyEdit/Config.cs or ~/.config/RudyEdit/Config.cs
RUDYEDIT_CONFIG_HOME can point to a separate absolute configuration folder.

Changing DefaultMode, DefaultKeysMode, OpenFileManager, ShowTagBar, or ShowConsole changes startup defaults. Use the Mode/View menus to change the current session. Line numbers, indentation, colours, spell checking, and layout preferences reload immediately.


Version 2.0.0 — Text mode bars

Formatting bar: Font and Size drop-downs, Bold, Italic, Underline, Text Color, Background Color, Bulleted/Ordered Lists, Spelling, Auto Spell, and Preview. Format opens every control on narrow terminals. Ctrl+Alt+M focuses formatting; arrows select and Enter activates. Font/size remain document markup; terminal cell fonts are host-controlled.

Current-file tags: bottom tag list plus inline Add tags field. Ctrl+Alt+T focuses entry. Enter adds comma-separated tags; Escape returns to editing. Click the tag list to edit/view all tags. Draft entries belong to each tab. Tags are undoable, deduplicated without case, and saved to Tags.xml with Ctrl+S. Ctrl+Shift+T toggles the bar.

File Manager: bottom Tag? field, Ctrl+Alt+G to focus; Enter searches saved Markdown/text files recursively in the displayed folder, matching all comma-separated tags without case. Empty query or × clears results. Search limit: 10,000 paths, 2 MB per file; symlinks/inaccessible files are skipped. All bars are Text-mode only.


Version 2.0.1 — Tag visibility and external storage

Ctrl+Shift+T toggles the current-file tag bar in Text mode in both Code and VIM keybinding modes; hiding it restores two document rows. Ctrl+Alt+T shows the bar and focuses tag entry. View → Toggle Tag Bar and the command palette provide the same action. ShowTagBar = true/false in Config.cs sets startup visibility. Hiding the bar preserves tags and unfinished entries. File Manager tag search remains available when this bar is hidden.

Tag names are stored only in Tags.xml next to Config.cs and KeyboardShortcuts.xml (Windows: %APPDATA%\RudyEdit\Tags.xml; Linux: $XDG_CONFIG_HOME/RudyEdit/Tags.xml or ~/.config/RudyEdit/Tags.xml; RUDYEDIT_CONFIG_HOME overrides the folder). Ctrl+S saves both the file link and its XML tags; unsaved tag changes participate in Save/Discard/Cancel and undo/redo. The configuration file is created on first launch without replacing an existing registry.

Each tagged file stores only an opaque <!-- rudyedit-id: UUID --> comment. RudyEdit removes that metadata from its editing view and reinserts it on save, so typing, selecting, copying, and previewing the document do not expose the code. Markdown renderers hide the comment; raw text tools can display it. Moving/renaming files retains their tags; Save As assigns an independent identifier. Removing all tags leaves an empty registry entry. Search resolves identifiers through Tags.xml and still finds legacy tag headers pending migration.

Legacy leading <!-- tags: ... --> headers are staged for migration when opened: the editor displays the document body and its tags, and the next save moves names to XML and replaces the old header with the identifier. No unopened documents are rewritten. Back up Tags.xml with the documents; a file identifier by itself cannot reconstruct tag names on another machine. Corrupt XML and conflicting tag edits cause a save error instead of overwriting the registry. The registry uses <Tags Version="1"><File Id="UUID" Path="..."><Tag>name</Tag></File></Tags>; UUID is a real GUID and tag text is XML escaped. Tags.xml is validated before saving from RudyEdit.


Version 2.0.3 — Error logging, configuration folder, and one Windows installer

Ctrl+Alt+C (Open Config Folder in Help) shows the configuration directory in RudyEdit File Manager and requests Explorer on Windows or the desktop file manager through xdg-open on Linux. It works in Code/VIM and Text/Development modes; the in-app folder remains usable when no desktop opener is available. Existing user keybinding files receive the shortcut through the default catalog unless the global sequence has been customized. Both distributed XML keybinding files include it explicitly.

RudyEdit-errors.log is created beside Config.cs, KeyboardShortcuts.xml, Tags.xml and languages.json. It records UTC timestamps, process IDs, context, exceptions/stacks and nested errors, startup/configuration/save/command failures, tool stderr and nonzero exits, compiler/LSP diagnostics, and debugger/protocol/background failures. Help → Error Log displays a searchable snapshot. The log rotates at 5 MiB with three backups; very long entries are marked/truncated at 65,536 characters. If the config folder is unwritable, the active log falls back to RudyEdit-<user>/RudyEdit-errors.log under the system temp directory. Fatal errors report the active path. Normal cancellations and cleanup races are excluded.

Windows users install with the single self-contained RudyEdit-2.0.3-Windows-Setup.exe. No separate MSI or .NET runtime download is needed. It preserves the binder-and-pen icon, optional Desktop shortcut, Start Menu entry, PATH update, bundled runtime/font, and existing upgrade identity. Windows Installer's verbose installation log remains at %TEMP%\RudyEdit-install.log. The internal MSI is a build intermediate, not an additional user installer.
