namespace RudyEdit;

public sealed record UiIcon(string Name,string Glyph,string Plain,string Color);

/// <summary>Single-cell glyphs verified against the bundled FiraCode Nerd Font Mono.</summary>
public static class UiIcons
{
    public const string FontFamily="FiraCode Nerd Font Mono";
    static UiIcon I(string name,int code,string plain,string color)=>new(name,char.ConvertFromUtf32(code),plain,color);
    public static readonly UiIcon Folder=I("Folder",0xf07b,"[D]","#e6bc78"),FolderOpen=I("Open folder",0xf07c,"[D]","#f4cc88"),Parent=I("Parent folder",0xf062,"[..]","#e6bc78"),
        GitFolder=I("Git folder",0xe5fb,"[G]","#ef997e"),GitHubFolder=I("GitHub folder",0xe5fd,"[G]","#c3a6ef"),ConfigFolder=I("Config folder",0xe5fc,"[=]","#a8b9ca"),NpmFolder=I("Dependencies folder",0xe5fa,"[N]","#ef8c84"),SourceFolder=I("Source folder",0xf114,"[S]","#80c9ff"),
        File=I("File",0xf15b,"[F]","#c2cfe0"),Text=I("Text",0xf15c,"[T]","#d8e4ef"),Markdown=I("Markdown",0xe73e,"[MD]","#80c9ff"),Code=I("Source code",0xf121,"[<>]","#82d4b3"),Config=I("Configuration",0xe615,"[=]","#a8b9ca"),
        CSharp=I("C#",0xe7b2,"[C#]","#c3a6ef"),C=I("C",0xe61e,"[C]","#80c9ff"),Cpp=I("C++",0xe61d,"[C+]","#80c9ff"),Python=I("Python",0xe73c,"[PY]","#e8cd83"),Rust=I("Rust",0xe7a8,"[RS]","#e5a178"),
        JavaScript=I("JavaScript",0xe60c,"[JS]","#ead07c"),TypeScript=I("TypeScript",0xe628,"[TS]","#80b4ec"),React=I("React",0xe625,"[RX]","#8cdce9"),Vue=I("Vue",0xe6a0,"[VU]","#82d4b3"),Html=I("HTML",0xe736,"[HT]","#ef997e"),Css=I("CSS",0xe749,"[CS]","#80b4ec"),Sass=I("Sass",0xe603,"[SC]","#e5a6ce"),Less=I("Less",0xe60b,"[LS]","#80b4ec"),
        Php=I("PHP",0xe73d,"[PH]","#b8a7eb"),Ruby=I("Ruby",0xe739,"[RB]","#ef9999"),Json=I("JSON",0xe80b,"[JSN]","#ead07c"),Xml=I("XML",0xe619,"[XML]","#e5a178"),Database=I("Database",0xe706,"[DB]","#8cdce9"),Prisma=I("Prisma",0xe684,"[PR]","#b8a7eb"),Docker=I("Docker",0xe7b0,"[DK]","#80c9ff"),Git=I("Git",0xe702,"[G]","#ef997e"),
        Terminal=I("Shell",0xf489,"[SH]","#82d4b3"),PowerShell=I("PowerShell",0xebc7,"[PS]","#80c9ff"),Log=I("Log",0xf022,"[LOG]","#d9b88e"),Lock=I("Lock file",0xf023,"[L]","#d9b88e"),Image=I("Image",0xe60d,"[IMG]","#b5cf86"),Archive=I("Archive",0xf1c6,"[ZIP]","#e6bc78"),Pdf=I("PDF",0xf1c1,"[PDF]","#ef9999"),
        Word=I("Word document",0xf1c2,"[DOC]","#80b4ec"),Spreadsheet=I("Spreadsheet",0xf1c3,"[XLS]","#82d4b3"),Presentation=I("Presentation",0xf1c4,"[PPT]","#e5a178"),Audio=I("Audio",0xe638,"[AUD]","#8cdce9"),Video=I("Video",0xe69f,"[VID]","#c3a6ef"),Binary=I("Binary",0xf085,"[BIN]","#a8b9ca"),
        Edit=I("Edit",0xf044,"","#80c9ff"),Mode=I("Mode",0xf074,"","#c3a6ef"),View=I("View",0xf06e,"","#8cdce9"),Build=I("Build",0xf0ad,"","#e6bc78"),Help=I("Help",0xf059,"","#80c9ff"),
        Font=I("Font",0xf031,"F","#d8e4ef"),Size=I("Font size",0xf034,"S","#d8e4ef"),Bold=I("Bold",0xf032,"B","#d8e4ef"),Italic=I("Italic",0xf033,"I","#d8e4ef"),Underline=I("Underline",0xf0cd,"U","#d8e4ef"),Color=I("Text color",0xf1fb,"A","#e6bc78"),Background=I("Background color",0xf1fc,"#","#82d4b3"),Bullets=I("Bullets",0xf0ca,"*","#d8e4ef"),Ordered=I("Numbered list",0xf0cb,"1.","#d8e4ef"),Spelling=I("Spelling",0xefb3,"ABC","#82d4b3"),Check=I("Auto spell",0xf046,"+","#82d4b3"),Palette=I("Formatting",0xefcc,"","#d9a9ef"),Tags=I("Tags",0xf02c,"#","#80c9ff"),Search=I("Search",0xf002,"?","#80c9ff"),
        Save=I("Save",0xf0c7,"","#82d4b3"),Close=I("Close",0xf00d,"","#ef9999"),Undo=I("Undo",0xf0e2,"","#80c9ff"),Redo=I("Redo",0xf01e,"","#80c9ff"),Copy=I("Copy",0xf0c5,"","#d8e4ef"),Cut=I("Cut",0xf0c4,"","#d8e4ef"),Paste=I("Paste",0xf0ea,"","#d8e4ef"),Run=I("Run",0xf04b,"","#82d4b3"),Debug=I("Debug",0xf188,"","#d9a9ef"),Stop=I("Stop",0xf04d,"","#ef9999"),Warning=I("Error log",0xf071,"!","#e6bc78"),Keyboard=I("Keyboard",0xf11c,"","#d8e4ef");
    public static IEnumerable<UiIcon> All=>typeof(UiIcons).GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static).Where(f=>f.FieldType==typeof(UiIcon)).Select(f=>(UiIcon)f.GetValue(null)!);
    public static UiIcon Menu(string name)=>name switch{"File"=>Folder,"Edit"=>Edit,"Mode"=>Mode,"Text"=>Font,"View"=>View,"Build"=>Build,_=>Help};
    public static UiIcon Command(string id)=>id switch
    {
        "font"=>Font,"font-size"=>Size,"bold"=>Bold,"italic"=>Italic,"underline"=>Underline,"foreground"=>Color,"background"=>Background,"bullets"=>Bullets,"ordered"=>Ordered,"spell"=>Spelling,"toggle-spell"=>Check,"preview"=>View,"format-menu" or "format-bar"=>Palette,
        "tags" or "tag-entry" or "toggle-tag-bar"=>Tags,"search-tags" or "find" or "find-next" or "replace" or "fm-find"=>Search,
        "new" or "fm-new"=>File,"open" or "folder"=>FolderOpen,"save" or "save-as" or "save-all"=>Save,"quit" or "close-tab" or "close-pane" or "close-other-panes" or "close-other-tabs"=>Close,
        "undo"=>Undo,"redo"=>Redo,"copy" or "fm-copy"=>Copy,"cut" or "fm-move"=>Cut,"paste" or "fm-paste"=>Paste,
        "build"=>Build,"run"=>Run,"debug" or "breakpoint"=>Debug,"stop"=>Stop,"diagnostics" or "error-log"=>Warning,
        "terminal" or "console"=>Terminal,"files"=>Folder,"config-folder"=>ConfigFolder,"edit-config"=>Config,"edit-keys" or "shortcuts" or "keys-code" or "keys-vim" or "toggle-keys"=>Keyboard,"manual"=>Markdown,"font-icons" or "help"=>Help,"toggle-icons"=>Image,
        _=>Code
    };
    public static UiIcon Entry(string path,bool directory=false,bool parent=false,bool opened=false)
    {
        if(parent)return Parent;
        string name=Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)).ToLowerInvariant();
        if(directory)return name switch
        {".git"=>GitFolder,".github" or ".gitlab"=>GitHubFolder,"node_modules"=>NpmFolder,".config" or "config" or "configuration"=>ConfigFolder,"src" or "source" or "lib"=>SourceFolder,_=>opened?FolderOpen:Folder};
        if(name is ".gitignore" or ".gitattributes" or ".gitmodules")return Git;
        if(name=="dockerfile"||name.StartsWith("dockerfile.",StringComparison.Ordinal)||name==".dockerignore")return Docker;
        if(name is "cargo.toml" or "cargo.lock")return Rust;
        if(name is "config.cs" or "keyboardshortcuts.xml" or "tags.xml" or "languages.json" or "tsconfig.json" or "settings.json"||name.StartsWith(".env",StringComparison.Ordinal))return Config;
        return Path.GetExtension(name) switch
        {
            ".cs" or ".csx" or ".csproj" or ".sln"=>CSharp,".c" or ".h"=>C,".cc" or ".cpp" or ".hpp" or ".cxx"=>Cpp,".py" or ".pyw"=>Python,".rs"=>Rust,
            ".js" or ".mjs" or ".cjs"=>JavaScript,".ts" or ".mts" or ".cts"=>TypeScript,".jsx" or ".tsx"=>React,".vue"=>Vue,".html" or ".htm"=>Html,".css"=>Css,".scss" or ".sass"=>Sass,".less"=>Less,".php"=>Php,".rb"=>Ruby,
            ".md" or ".markdown" or ".mdx"=>Markdown,".txt" or ".text"=>Text,".json" or ".jsonc"=>Json,".xml" or ".xaml" or ".xsd"=>Xml,".yaml" or ".yml" or ".toml" or ".ini" or ".conf" or ".config"=>Config,".prisma"=>Prisma,
            ".sql" or ".sqlite" or ".sqlite3" or ".db"=>Database,".sh" or ".bash" or ".zsh" or ".fish" or ".bat" or ".cmd"=>Terminal,".ps1" or ".psm1"=>PowerShell,".log"=>Log,".lock"=>Lock,
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".ico" or ".webp" or ".bmp" or ".avif"=>Image,".zip" or ".7z" or ".tar" or ".gz" or ".bz2" or ".xz" or ".zst" or ".rar"=>Archive,".pdf"=>Pdf,
            ".doc" or ".docx" or ".odt" or ".rtf"=>Word,".csv" or ".xlsx" or ".xls" or ".ods"=>Spreadsheet,".ppt" or ".pptx" or ".odp"=>Presentation,
            ".mp3" or ".wav" or ".flac" or ".ogg" or ".m4a"=>Audio,".mp4" or ".mkv" or ".mov" or ".webm" or ".avi"=>Video,".ttf" or ".otf" or ".woff" or ".woff2"=>Font,".exe" or ".dll" or ".so" or ".bin" or ".appimage"=>Binary,
            _=>File
        };
    }
}
