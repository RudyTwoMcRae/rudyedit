using System.Text.RegularExpressions;
using System.Xml.Linq;
namespace RudyEdit;

public sealed class Settings
{
    public static string ConfigDirectory => Environment.GetEnvironmentVariable("RUDYEDIT_CONFIG_HOME") ?? Path.Combine(
        OperatingSystem.IsWindows() ? (Environment.GetEnvironmentVariable("APPDATA") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
        : (Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")), "RudyEdit");
    public static string ConfigPath => Path.Combine(ConfigDirectory, "Config.cs");
    public static string ErrorLogPath => Path.Combine(ConfigDirectory, "RudyEdit-errors.log");
    public static string TagsPath => Path.Combine(ConfigDirectory, "Tags.xml");
    public bool UseNerdIcons {get;set;} = true;
    public bool ShowTagBar {get;set;} = true;
    public static string KeysPath => Path.Combine(ConfigDirectory, "KeyboardShortcuts.xml");
    public bool LineNumbers { get; set; } = true;
    public bool OpenFileManager { get; set; }
    public string DefaultKeysMode { get; set; } = "Code";
    public string DefaultMode { get; set; } = "Text";
    public int DefaultTab { get; set; } = 5;
    public bool TabSpaces { get; set; }
    public int FileManagerWidth { get; set; } = 20;
    public int FontSize { get; set; } = 12;
    public int ConsoleHeight { get; set; } = 10;
    public bool TextConsole { get; set; }
    public bool DevelopmentConsole { get; set; } = true;
    public bool SpellCheck { get; set; } = true;
    public string Font { get; set; } = "FiraCode Nerd Font Mono";
    public string Foreground { get; set; } = "#d8dee9";
    public string Background { get; set; } = "#151c28";
    public static void EnsureDefaults()
    {
        Directory.CreateDirectory(ConfigDirectory);
        foreach (var f in new[] { "Config.cs", "KeyboardShortcuts.xml", "languages.json", "Tags.xml" })
        { var p = Path.Combine(ConfigDirectory, f); if (!File.Exists(p)) File.Copy(Path.Combine(AppContext.BaseDirectory, "assets", f), p); }
        try { KeyBindings.UpgradeLegacyCloseBindings(KeysPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException) { ErrorLog.Write(ex,"Upgrade legacy keybindings"); }
    }
    public static Settings Load(string path)
    {
        var s = new Settings(); int branch = 0, lineNo = 0;
        foreach (string source in File.ReadLines(path))
        {
            lineNo++; var line = source.Split("//")[0].Trim();
            if (line.Length == 0 || line is "{" or "}") continue;
            if (line == "if (Mode == Development)") { branch = 1; continue; }
            if (line == "else") { branch = 2; continue; }
            var m = Regex.Match(line, "^(\\w+)\\s*=\\s*(\"[^\"]*\"|[^;]+);$");
            if (!m.Success) throw new FormatException($"Config.cs line {lineNo}: expected Setting = value;");
            string key = m.Groups[1].Value, value = m.Groups[2].Value.Trim().Trim('"');
            bool B() => bool.TryParse(value, out bool v) ? v : throw new FormatException($"{key} must be true or false (line {lineNo}).");
            int N(int min, int max) => int.TryParse(value, out int v) && v >= min && v <= max ? v : throw new FormatException($"{key} must be between {min} and {max} (line {lineNo}).");
            switch (key)
            {
                case "UseNerdIcons": s.UseNerdIcons = B(); break;
                case "ShowTagBar": s.ShowTagBar = B(); break;
                case "LineNumbers": s.LineNumbers = B(); break;
                case "OpenFileManager": s.OpenFileManager = B(); break;
                case "DefaultKeysMode": if (value is not ("Code" or "VIM")) throw new FormatException("DefaultKeysMode must be Code or VIM."); s.DefaultKeysMode = value; break;
                case "DefaultMode": if (value is not ("Text" or "Development")) throw new FormatException("DefaultMode must be Text or Development."); s.DefaultMode = value; break;
                case "DefaultTab": s.DefaultTab = N(1, 16); break;
                case "TabSpaces": s.TabSpaces = B(); break;
                case "FileManagerWidth": s.FileManagerWidth = N(10, 50); break;
                case "FontSize": s.FontSize = N(6, 96); break;
                case "ConsoleHeight": s.ConsoleHeight = N(10, 70); break;
                case "ShowConsole": if (branch == 1) s.DevelopmentConsole = B(); else if (branch == 2) s.TextConsole = B(); else { s.TextConsole = s.DevelopmentConsole = B(); } break;
                case "SpellCheck": s.SpellCheck = B(); break;
                case "Font": s.Font = value; break;
                case "Foreground": s.Foreground = Color(value); break;
                case "Background": s.Background = Color(value); break;
                default: throw new FormatException($"Unknown setting {key} (line {lineNo}).");
            }
        }
        return s;
    }
    static string Color(string value) => Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$") ? value : throw new FormatException("Use a colour such as #c0ffee.");
}
public sealed record Binding(string Command, string[] Keys, string Context, string Description)
{
    public string Display => string.Join(", ", Keys);
}
public static class KeyBindings
{
    public static string Normalize(string key)
    {
        key = key.Trim().ToLowerInvariant().Replace(" arrow", "").Replace("control+", "ctrl+").Replace("return", "enter").Replace("escape", "esc").Replace("spacebar", "space");
        var p = key.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string main = p.LastOrDefault() ?? "";
        return (p.Contains("ctrl") ? "ctrl+" : "") + (p.Contains("alt") ? "alt+" : "") + (p.Contains("shift") ? "shift+" : "") + main;
    }
    public static void UpgradeLegacyCloseBindings(string path)
    {
        var xml = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        if (xml.Root?.Name != "KeyboardShortcuts") return;
        bool changed = false;
        foreach (var binding in xml.Root.Elements("Binding"))
        {
            string name = binding.Element("Name")?.Value.Trim() ?? "";
            string key = Normalize(binding.Element("Keys")?.Value ?? "");
            string? replacement = (name, key) switch
            {
                ("Close Panel" or "close-pane", "ctrl+shift+w") => "Close Tab",
                ("Close Tab" or "close-tab", "ctrl+w") => "Close Panel",
                _ => null
            };
            if (replacement == null) continue;
            binding.Element("Name")!.Value = replacement;
            binding.SetElementValue("Description", replacement == "Close Tab"
                ? "Remove the current tab from its panel after checking unsaved changes."
                : "Remove the current panel after checking all its unsaved tabs.");
            changed = true;
        }
        if (!changed) return;
        string temporary = path + ".upgrade-" + Guid.NewGuid().ToString("N");
        try { xml.Save(temporary, SaveOptions.DisableFormatting); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static List<Binding> Load(string path)
    {
        var root = XDocument.Load(path).Root;
        if (root?.Name != "KeyboardShortcuts") throw new FormatException("Expected <KeyboardShortcuts> root.");
        var result = new List<Binding>(); var seen = new HashSet<string>();
        foreach (var b in root.Elements("Binding"))
        {
            string name = b.Element("Name")?.Value.Trim() ?? "", context = b.Element("Context")?.Value.Trim() ?? "Global";
            var keys = (b.Element("Keys")?.Value ?? "").Split(',').Select(Normalize).ToArray();
            if (keys.Any(string.IsNullOrWhiteSpace)) throw new FormatException("Binding has an empty key sequence.");
            string command = Commands.Resolve(name, context, keys);
            // Ctrl+W used to be the default Close Tab binding. Upgrade that
            // legacy mapping in memory while retaining the user's XML file.
            if (command == "close-tab" && keys.SequenceEqual(new[]{"ctrl+w"})) command = "close-pane";
            if (command == "close-pane" && keys.SequenceEqual(new[]{"ctrl+shift+w"})) command = "close-tab";
            if (!Commands.All.Any(c => c.Id == command)) throw new FormatException("Unknown command: " + name + ". Press F1 in the shortcut file for valid names.");
            if (context is not ("Global" or "Panel" or "Tab" or "File Manager" or "Text" or "Development")) throw new FormatException("Unknown shortcut context: " + context);
            if (!seen.Add(context + ":" + string.Join(',', keys))) throw new FormatException("Duplicate shortcut: " + context + " " + string.Join(',', keys));
            result.Add(new(command, keys, context, b.Element("Description")?.Value ?? ""));
        }
        foreach (var c in Commands.WithDefaultShortcuts)
        {
            var keys = c.Shortcut.Split(',').Select(Normalize).ToArray();
            if (!result.Any(b => b.Context == "Global" && b.Keys.SequenceEqual(keys))) result.Add(new(c.Id, keys, "Global", c.Description));
        }
        return result;
    }
}
public sealed record Command(string Id, string Name, string Menu, string Shortcut, string Description, string Example, string Example2 = "");
public static class Commands
{
    public static readonly Command[] All =
    [
        new("new", "New File", "File", "Ctrl+n", "Create a document in a new tab.", "Ctrl+N, type a note, Ctrl+S.", "Create a new Python script; Save As hello.py."),
        new("open", "Open", "File", "Ctrl+o", "Open a file; dialog supports Ctrl+V split, Ctrl+H split, Ctrl+T tab.", "Open /home/rudy/notes.md.", "Press Ctrl+V in the file dialog for a vertical split."),
        new("folder", "Open Folder", "File", "Ctrl+Shift+o", "Change working folder and show its files.", "Choose C:\\Projects\\demo.", "Use rudyedit -p /home/rudy/project."),
        new("save", "Save", "File", "Ctrl+s", "Save the current document atomically as UTF-8.", "Ctrl+S saves changes.", "A new document asks for a path."),
        new("save-all", "Save All", "File", "Ctrl+Shift+s", "Save every open document; new files ask for paths.", "Save two modified tabs.", "Review any external modification warning."),
        new("save-as", "Save As", "File", "Ctrl+Alt+s", "Save under a new path; existing destinations require confirmation.", "Save As notes.md.", "Save a copy as notes-backup.md."),
        new("folder", "Load", "File", "", "Load a working folder.", "Load a different project."),
        new("close-tab", "Close Tab", "File", "Ctrl+Shift+w", "Remove the current tab from its panel, prompting for unsaved changes.", "Ctrl+Shift+W removes the active tab and selects a remaining tab.", "Choose Save when prompted for a changed tab."),
        new("close-other-tabs", "Close Other Tabs", "File", "", "Close other tabs in the current pane with save prompts.", "Keep only the current tab."),
        new("quit", "Quit", "File", "Ctrl+q", "Exit after checking every unsaved document.", "Ctrl+Q and choose Save or Discard."),
        new("keys-code", "VS Code", "Mode", "", "Use familiar Code-style navigation and editing.", "F2 toggles keybinding style."),
        new("keys-vim", "VIM", "Mode", "", "Use Normal, Insert, Visual, and command modes.", "i to insert; Esc returns to Normal.", ":w saves, :q closes, :wq saves and closes; dd deletes a line."),
        new("text-mode", "Text", "Mode", "", "Use Markdown notes, tags, spelling, and formatting.", "Write a note with **bold** text."),
        new("dev-mode", "Development", "Mode", "", "Show code tools, completion, diagnostics, and build/debug menu.", "Open hello.py then press F3."),
        new("toggle-keys", "Toggle Keys Mode", "", "F2", "Toggle Code and VIM keybindings.", "F2 changes the status indicator."),
        new("toggle-mode", "Toggle Developer Mode", "", "F3", "Toggle Text and Development modes.", "F3 shows the Build menu."),
        new("bold", "Bold", "Text", "Ctrl+b", "Wrap selection or future text in Markdown bold markers.", "Select a word then Ctrl+B.", "Ctrl+B, type, Right twice to leave the markers."),
        new("italic", "Italic", "Text", "Ctrl+i", "Wrap text in Markdown emphasis.", "Select text and choose Italic."),
        new("underline", "Underline", "Text", "Ctrl+u", "Wrap text with HTML <u> supported by HTML-aware Markdown readers.", "Select a heading and choose Underline."),
        new("font", "Font", "Text", "", "Choose an installed Nerd Font for an HTML span; terminal font is host-controlled.", "Choose FiraCode Nerd Font Mono."),
        new("font-size", "Font Size", "Text", "", "Set a document span's point size; visible in HTML-capable Markdown readers.", "Set a title to 18 pt."),
        new("foreground", "Text Color", "Text", "Ctrl+Alt+f", "Choose a hex colour for selected or future text.", "Select text and choose #ffcc66."),
        new("background", "Background Color", "Text", "Ctrl+Alt+b", "Choose the editor background colour for this session.", "Choose #152030."),
        new("bullets", "Bulleted List", "Text", "", "Prefix selected lines with Markdown bullets.", "Select three lines and choose Bulleted List."),
        new("ordered", "Ordered List", "Text", "", "Number selected lines.", "Select tasks and choose Ordered List."),
        new("toggle-tag-bar", "Toggle Tag Bar", "View", "Ctrl+Shift+t", "Show or hide the current-file tag bar in Text mode.", "Ctrl+Shift+T hides the tag bar and restores two rows of document space."),
        new("tag-entry", "Add Tags", "Text", "Ctrl+Alt+t", "Focus the bottom tag-entry bar in Text mode. Enter adds comma-separated tags; Escape returns to the document.", "Add work, project then press Enter and Ctrl+S."),
        new("format-bar", "Formatting Bar", "Text", "Ctrl+Alt+m", "Focus the formatting bar. Left/Right choose a control; Enter activates it; Escape returns to the document.", "Choose Bold or open the Font and Size drop-downs."),
        new("tags", "Tags", "Text", "", "Set comma-separated tags in Tags.xml; the saved document contains a hidden identifier only.", "Add work, meetings; use Search Tags to find it."),
        new("search-tags", "Search Tags", "Text", "Ctrl+Alt+g", "Search Markdown/text files under the working folder by tag.", "Search tag work."),
        new("spell", "Check Spelling", "Text", "F7", "Check words with the installed English dictionary; suggest replacements.", "F7 opens misspellings; Enter shows suggestions."),
        new("toggle-spell", "Spell Check As You Type", "Text", "", "Toggle underlined unknown words in Text mode.", "Type a misspelled word to see an underline."),
        new("preview", "Markdown Preview", "Text", "Ctrl+Shift+m", "Show a readable, styled terminal preview of the current Markdown.", "Preview a note after adding a list."),
        new("undo", "Undo", "Edit", "Ctrl+z", "Undo the previous edit.", "Delete a line, then undo."),
        new("redo", "Redo", "Edit", "Ctrl+y", "Redo an undone edit.", "Undo then Ctrl+Y."),
        new("copy", "Copy", "Edit", "Ctrl+c", "Copy the selection, or current line, to the internal clipboard.", "Shift+arrows to select; Ctrl+C.", "Use terminal copy to share text with another application."),
        new("cut", "Cut", "Edit", "Ctrl+x", "Cut selection, or current line, to the internal clipboard.", "Select a sentence then Ctrl+X."),
        new("paste", "Paste", "Edit", "Ctrl+v", "Paste internal clipboard; terminal paste is also supported.", "Ctrl+C then Ctrl+V."),
        new("select-all", "Select All", "Edit", "Ctrl+a", "Select the entire document.", "Ctrl+A then Ctrl+C."),
        new("find", "Find", "Edit", "Ctrl+f", "Find text from the cursor, wrapping at the end.", "Find TODO then F4 for the next match."),
        new("find-next", "Find Next", "Edit", "F4", "Repeat the previous find.", "F4 selects the next match."),
        new("replace", "Find and Replace", "Edit", "Ctrl+h", "Replace every exact occurrence after a confirmation.", "Replace old_name with new_name."),
        new("goto", "Go To Line", "Edit", "Ctrl+g", "Move to a one-based line number.", "Go to line 42."),
        new("complete", "Code Completion", "Build", "Ctrl+Space", "Use connected language server or local language keywords and file symbols.", "Type pri in Python and invoke completion."),
        new("build", "Build Project", "Build", "F6", "Build nearest .NET/Cargo project, check C, or compile Python bytecode.", "Open Program.cs then F6.", "Open main.c to compile with gcc -g."),
        new("run", "Run", "Build", "Ctrl+F5", "Run current project/script; console input goes to the running process.", "Run hello.py.", "Run a .NET console project."),
        new("debug", "Debug", "Build", "F5", "Start debugger or continue. Python uses stdlib bdb; C/Rust use gdb; C# uses netcoredbg DAP.", "Open a Python script and press F5, then F10 to step.", "Install netcoredbg for C# and choose a built DLL."),
        new("step-over", "Step Over", "Build", "F10", "Step the paused debug target over the current line.", "In a paused debugger press F10."),
        new("step-in", "Step Into", "Build", "F11", "Step into a function at the current line.", "Press F11 at a function call."),
        new("step-out", "Step Out", "Build", "Shift+F11", "Run until the current function returns.", "Press Shift+F11 inside a function."),
        new("breakpoint", "Toggle Breakpoint", "Build", "F9", "Toggle a breakpoint on the current line.", "Move to a line and press F9."),
        new("variables", "Variables", "Build", "", "Inspect local variables when the debugger is paused.", "Pause at a line, then inspect Variables."),
        new("stop", "Stop", "Build", "Shift+F5", "Stop the active process, debugger, or language server.", "Stop a running loop with Shift+F5."),
        new("diagnostics", "Errors and Warnings", "Build", "Ctrl+Shift+e", "Show build or language server diagnostics; select an item to jump.", "Build invalid code then open Errors and Warnings."),
        new("lsp", "Connect Language Server", "Build", "", "Connect the configured server from languages.json for richer completions and live diagnostics.", "Install pylsp then connect from a Python file."),
        new("terminal", "Focus Console", "View", "Ctrl+`", "Focus the embedded command runner and process input; Esc returns to editor.", "Type git status then Enter."),
        new("console", "Toggle The Console", "View", "", "Show or hide console output.", "Ctrl+K, C toggles the console."),
        new("files", "Toggle File Manager Panel", "View", "Shift+Space", "Show/hide file manager.", "Shift+Space opens files; Enter opens; v/h split; t new tab.", "Some terminals need enhanced key encoding; F10 View offers a fallback."),
        new("vertical", "Vertical Split", "View", "Ctrl+Alt+v", "Split current pane vertically.", "Split then open a second file."),
        new("horizontal", "Horizontal Split", "View", "Ctrl+Alt+h", "Split current pane horizontally.", "Split to compare two regions."),
        new("close-pane", "Close Panel", "View", "Ctrl+w", "Close the current pane after checking unsaved tabs.", "Ctrl+W closes an unneeded split.", "Choose Cancel at the unsaved prompt to keep the panel open."),
        new("close-other-panes", "Close Other Panels", "View", "", "Keep only the current pane, checking all other tabs.", "Return to a single editor pane."),
        new("next-pane", "Go To Next Panel", "View", "Alt+Right", "Focus the next pane.", "Ctrl+K, P, Right is the supplied alternative."),
        new("previous-pane", "Go To Previous Panel", "View", "Alt+Left", "Focus the previous pane.", "Alt+Left returns to the previous pane."),
        new("next-tab", "Go To Next Tab", "View", "Ctrl+Alt+Right", "Select the next tab in this pane.", "Ctrl+Alt+Right moves to the next tab."),
        new("previous-tab", "Go To Previous Tab", "View", "Ctrl+Alt+Left", "Select the previous tab.", "Ctrl+Alt+Left returns to the previous tab."),
        new("toggle-icons", "Toggle Graphic Icons", "View", "", "Switch between Nerd Font glyphs and plain file-type labels for this session.", "Use plain labels when your terminal font does not contain Nerd Font symbols."),
        new("font-icons", "Nerd Font & Icons", "Help", "", "Show terminal-font setup and the file-icon legend.", "Select FiraCode Nerd Font Mono in your terminal and keep UseNerdIcons = true in Config.cs."),
        new("help", "Show Available Commands", "Help", "F1", "Show searchable contextual help, with descriptions and examples.", "F1 inside Config.cs explains every setting."),
        new("shortcuts", "Show Keybindings", "Help", "Shift+F1", "Show all configured shortcuts and current keybinding mode.", "Search for split to find split commands."),
        new("edit-keys", "Keybindings", "Help", "Shift+F2", "Open the user shortcut XML file; save reloads valid bindings.", "Add an existing command name and key sequence."),
        new("config-folder", "Open Config Folder", "Help", "Ctrl+Alt+c", "Open the configuration folder in the system file manager and RudyEdit File Manager.", "Ctrl+Alt+C opens Config.cs, KeyboardShortcuts.xml, Tags.xml, languages.json, and the error log folder."),
        new("error-log", "Error Log", "Help", "", "Open the active persistent error log, including a temporary-folder fallback if needed.", "Help → Error Log shows errors, timestamps, context, and exception stacks."),
        new("edit-config", "Config", "Help", "Ctrl+F2", "Open the user Config.cs settings DSL; save reloads valid settings.", "Change DefaultTab = 4; then save."),
        new("manual", "Language Manual", "Help", "Ctrl+F1", "Read the bundled searchable offline guide for this file's language/framework.", "Open main.py then Ctrl+F1."),
        new("palette", "Command Palette", "Help", "Ctrl+Shift+p", "Search and run any available command.", "Search Save As then Enter."),
        new("fm-open", "Open in Panel", "", "", "Open selected file in current panel.", "File Manager: Enter."),
        new("fm-tab", "Tab", "", "", "Open selected file in new tab.", "File Manager: t."),
        new("fm-find", "Find File", "", "", "Filter file names in current folder.", "File Manager: Ctrl+F."),
        new("fm-new", "Create File", "", "", "Create a named file in the current folder.", "File Manager: n."),
        new("fm-copy", "Copy File", "", "", "Copy a file/directory path for paste.", "File Manager: y then p in the destination."),
        new("fm-move", "Move File", "", "", "Cut a file/directory path for paste.", "File Manager: d then p."),
        new("fm-paste", "Paste File", "", "", "Copy/move without overwriting an existing path.", "File Manager: p."),
        new("fm-folder", "New Folder", "", "", "Create a subfolder.", "File Manager: f.")
    ];
    public static IEnumerable<Command> WithDefaultShortcuts => All.Where(c => c.Shortcut.Length > 0);
    public static string Resolve(string name, string context, string[] keys)
    {
        if (name == "Help") return keys.Contains("shift+f1") ? "shortcuts" : "help";
        if (context == "File Manager") return name switch { "Find" => "fm-find", "New File" => "fm-new", "Copy" => "fm-copy", "Move" => "fm-move", "Paste" => "fm-paste", "Load Folder" => "folder", _ => All.FirstOrDefault(c => c.Name == name || c.Id == name)?.Id ?? name };
        return All.FirstOrDefault(c => c.Name == name || c.Id == name)?.Id ?? name;
    }
}
