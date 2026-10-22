using System.Net;
using System.Text.RegularExpressions;
namespace RudyEdit;
public sealed partial class EditorApp
{
    void Execute(string id)
    {
        switch(id)
        {
            case "new":active.Tabs.Add(new());active.Selected=active.Tabs.Count-1;filesFocus=consoleFocus=false;break;
            case "open":FilePrompt();break;case "folder":Prompt("Working folder",working,(text,_)=>{var p=Absolute(text);if(!Directory.Exists(p))throw new DirectoryNotFoundException(p);working=p;browser.Load(p);showFiles=filesFocus=true;consoleFocus=false;server?.Dispose();server=null;});break;
            case "save":Save(Doc,false);break;case "save-as":Save(Doc,true);break;case "save-all":SaveAll();break;
            case "close-tab":CloseTab();break;case "close-other-tabs":var keepDoc=Doc;foreach(var d in active.Tabs.Where(d=>d!=keepDoc).ToArray())if(MayClose(d))active.Tabs.Remove(d);active.Selected=active.Tabs.IndexOf(keepDoc);break;
            case "quit":Quit();break;
            case "toggle-keys":keysMode=keysMode=="VIM"?"Code":"VIM";vim="NORMAL";message="Keys: "+keysMode;break;
            case "keys-vim":keysMode="VIM";vim="NORMAL";break;case "keys-code":keysMode="Code";break;
            case "toggle-mode":SetMode(mode=="Text"?"Development":"Text");break;case "text-mode":SetMode("Text");break;case "dev-mode":SetMode("Development");break;
            case "files":showFiles=!showFiles;filesFocus=showFiles;consoleFocus=false;if(showFiles)browser.Refresh();break;
            case "console":showConsole=!showConsole;if(!showConsole)consoleFocus=false;break;case "terminal":showConsole=consoleFocus=true;filesFocus=false;break;
            case "vertical":if(filesFocus)OpenSelected("vertical");else Split(true);break;case "horizontal":if(filesFocus)OpenSelected("horizontal");else Split(false);break;
            case "close-pane":ClosePane(active);break;case "close-other-panes":var keep=active;foreach(var p in layout.Panes.Where(p=>p!=keep).ToArray())ClosePane(p);active=keep;break;
            case "next-pane":NextPane(1);break;case "previous-pane":NextPane(-1);break;case "next-tab":Tab(1);break;case "previous-tab":Tab(-1);break;
            case "bold":Doc.Wrap("**","**");Changed();break;case "italic":Doc.Wrap("*","*");Changed();break;case "underline":Doc.Wrap("<u>","</u>");Changed();break;
            case "font":Choose("Document font — terminal font is configured in your terminal",InstalledFonts().Select(f=>new Choice(f,f)).ToList(),f=>{Doc.Wrap("<span style=\"font-family:'"+WebUtility.HtmlEncode(f)+"'\">","</span>");Changed();});break;
            case "font-size":Prompt("Document font size (6–96 pt)",settings.FontSize.ToString(),(text,_)=>{if(!int.TryParse(text,out int n)||n<6||n>96)throw new FormatException("Choose 6–96 pt.");Doc.Wrap($"<span style=\"font-size:{n}pt\">","</span>");Changed();});break;
            case "foreground":Colour("Text colour",foreground,c=>{foreground=c;Doc.Wrap("<span style=\"color:"+c+"\">","</span>");Changed();});break;
            case "background":Colour("Editor background",background,c=>background=c);break;
            case "bullets":Doc.PrefixLines("- ");Changed();break;case "ordered":Doc.PrefixLines("",true);Changed();break;
            case "undo":Doc.Undo();Changed();break;case "redo":Doc.Redo();Changed();break;
            case "copy":clipboard=SelectedText();message="Copied to RudyEdit clipboard.";break;
            case "cut":clipboard=SelectedText();if(Doc.Selection.Length>0)Doc.Insert("");else CutLine();Changed();break;
            case "paste":Doc.Insert(clipboard);Changed();break;case "select-all":Doc.Anchor=0;Doc.Cursor=Doc.Text.Length;break;
            case "find":if(filesFocus){Execute("fm-find");break;}Prompt("Find text",find,(text,_)=>{find=text;message=Doc.Find(text)?"Match selected — F4 next":"No match.";});break;
            case "find-next":message=Doc.Find(find)?"Match selected — F4 next":"No match.";break;
            case "replace":Prompt("Find and replace — exact text","",(text,_)=>{if(text.Length==0)return;Prompt("Replace with","",(replacement,_)=>{int count=Regex.Matches(Doc.Text,Regex.Escape(text)).Count;if(count==0){message="No matches.";return;}if(Confirm($"Replace {count} occurrence(s)?",false)){Doc.Replace(0,Doc.Text.Length,Doc.Text.Replace(text,replacement,StringComparison.Ordinal));Changed();}});});break;
            case "goto":Prompt("Go to line (1-based)",(Doc.Position.Row+1).ToString(),(text,_)=>{if(!int.TryParse(text,out int n)||n<1||n>Doc.Lines.Length)throw new FormatException("Line is outside this document.");Doc.Anchor=null;Doc.SetPosition(n-1,0);});break;
            case "toggle-tag-bar":if(mode=="Text"){showTagBar=!showTagBar;if(!showTagBar&&barFocus=="tags")barFocus="";message="Tag bar "+(showTagBar?"shown.":"hidden.");}break;
            case "tag-entry":if(mode=="Text")FocusBar("tags");break;case "format-bar":if(mode=="Text")FocusBar("format");break;
            case "tags":EditTags();break;case "search-tags":SearchTags();break;case "preview":Preview();break;
            case "spell":CheckSpelling();break;case "toggle-spell":settings.SpellCheck=!settings.SpellCheck;message="Spelling while typing: "+settings.SpellCheck;break;
            case "help":Help();break;case "shortcuts":ShowText("Keybindings — "+keysMode,string.Join("\n\n",bindings.Select(b=>$"{b.Display} [{b.Context}] → {Commands.All.First(c=>c.Id==b.Command).Name}\n{b.Description}\nExample: {Commands.All.First(c=>c.Id==b.Command).Example}"))+(keysMode=="VIM"?"\n\n"+VimHelp:""));break;
            case "config-folder":
                var configFolder=Path.GetFullPath(Settings.ConfigDirectory);Directory.CreateDirectory(configFolder);browser.Load(configFolder);tagQuery="";showFiles=filesFocus=true;consoleFocus=false;barFocus="";
                folderLauncher(configFolder);message="Configuration folder: "+configFolder;break;
            case "error-log":ErrorLog.WriteMessage("Error log opened","Opened from the Help menu","INFO");ShowText("Error log — "+ErrorLog.FilePath,ErrorLog.Read());break;
            case "toggle-icons":settings.UseNerdIcons=!settings.UseNerdIcons;message=settings.UseNerdIcons?"Nerd Font icons enabled — use FiraCode Nerd Font Mono in your terminal.":"Plain file-type labels enabled.";break;
            case "font-icons":ShowText("Nerd Font & Icons",File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"assets","font-icons-help.md"))+"\n\nICON LEGEND\n"+string.Join("\n",UiIcons.All.Select(i=>(settings.UseNerdIcons?i.Glyph:i.Plain)+"  "+i.Name)));break;
            case "edit-keys":Open(Settings.KeysPath,"tab");break;case "edit-config":Open(Settings.ConfigPath,"tab");break;
            case "manual":ShowManual();break;case "palette":Palette();break;
            case "complete":if(mode=="Text")Palette();else Complete();break;
            case "build":Build(false);break;case "run":Build(true);break;case "debug":Debug();break;
            case "step-over":debugger?.Resume("next");break;case "step-in":debugger?.Resume("step");break;case "step-out":debugger?.Resume("out");break;
            case "breakpoint":ToggleBreakpoint();break;case "variables":if(debugger==null)throw new IOException("Start debugging first.");debugger.Variables();break;
            case "stop":Stop();break;case "diagnostics":Choose("Errors and warnings",diagnostics.Select((d,i)=>new Choice($"{Path.GetFileName(d.File)}:{d.Line}:{d.Column} {d.Message}",i.ToString(),d.File)).ToList(),value=>{var d=diagnostics[int.Parse(value)];Open(d.File,"tab");Doc.SetPosition(d.Line-1,d.Column-1);});break;
            case "lsp":if(Doc.FilePath==null&&!Save(Doc,false))return;server?.Dispose();server=null;server=new(Doc.Language,working);server.Sync(Doc);lastServerText=Doc.Text;message="Language server connected for "+Languages.Get(Doc.Language).Name;break;
            case "fm-open":OpenSelected("current");break;case "fm-tab":OpenSelected("tab");break;
            case "fm-find":Prompt("Find file by name (current folder)",browser.Filter,(text,_)=>{browser.Filter=text;browser.Index=browser.Scroll=0;browser.Refresh();});break;
            case "fm-new":Prompt("New file name","",(text,_)=>{string path=Path.GetFullPath(text,browser.Folder);using var f=new FileStream(path,FileMode.CreateNew);browser.Refresh();});break;
            case "fm-folder":Prompt("New folder name","",(text,_)=>{string path=Path.GetFullPath(text,browser.Folder);if(Directory.Exists(path))throw new IOException("Folder already exists.");Directory.CreateDirectory(path);browser.Refresh();});break;
            case "fm-copy":case "fm-move":fileClipboard=browser.Selected??"";moveFile=id=="fm-move";message=(moveFile?"Cut ":"Copied ")+fileClipboard;break;
            case "fm-paste":if(fileClipboard.Length==0)throw new IOException("No copied file.");FileBrowser.CopyPath(fileClipboard,Path.Combine(browser.Folder,Path.GetFileName(fileClipboard)),moveFile);if(moveFile)fileClipboard="";browser.Refresh();break;
        }
    }
    void SetMode(string value){barFocus="";mode=value;showConsole=value=="Development"?settings.DevelopmentConsole:settings.TextConsole;if(!showConsole)consoleFocus=false;message="Mode: "+value;}
    bool SaveAll(){foreach(var d in layout.Panes.SelectMany(p=>p.Tabs).Distinct().Where(d=>d.Dirty||d.FilePath==null))if(!Save(d,false))return false;return true;}
    void Stop(){buildThenRun=null;runner.Stop();debugger?.Dispose();debugger=null;debugLine=0;debugFile=null;message="Stopped.";}
    void Build(bool run)
    {
        if(!SaveAll()||Doc.FilePath==null)return;SetMode("Development");showConsole=true;diagnostics.Clear();consoleScroll=0;
        // C needs a separate compilation before execution. Other toolchains build as part of run.
        if(run&&Doc.Language=="c"){buildThenRun=Doc.FilePath;runner.Start(BuildTools.Plan(Doc.FilePath,false));}
        else runner.Start(BuildTools.Plan(Doc.FilePath,run));
        message=run?"Program started — View → Focus Console for input":"Build started.";
    }
    void Debug()
    {
        if(debugger?.Active==true){debugger.Resume("continue");return;}
        if(!SaveAll()||Doc.FilePath==null)return;SetMode("Development");showConsole=true;
        string source=Doc.FilePath;string? target=null;
        if(Doc.Language!="python")
        {
            string proposed="";
            if(Doc.Language=="c")proposed=Path.ChangeExtension(source,OperatingSystem.IsWindows()?"exe":"out");
            if(Doc.Language=="rust"){var project=BuildTools.FindProject(source,"Cargo.toml");if(project!=null){var name=Regex.Match(File.ReadAllText(project),"name\\s*=\\s*\"([^\"]+)\"").Groups[1].Value;proposed=Path.Combine(Path.GetDirectoryName(project)!,"target","debug",name+(OperatingSystem.IsWindows()?".exe":""));}}
            if(Doc.Language=="csharp"){var project=BuildTools.FindProject(source,"*.csproj");if(project!=null){string bin=Path.Combine(Path.GetDirectoryName(project)!,"bin","Debug");if(Directory.Exists(bin))proposed=Directory.EnumerateFiles(bin,Path.GetFileNameWithoutExtension(project)+".dll",SearchOption.AllDirectories).FirstOrDefault(p=>!p.Contains(Path.DirectorySeparatorChar+"ref"+Path.DirectorySeparatorChar))??"";}}
            target=PromptSync("Debug target — build with F6 first (.NET: DLL; C/Rust: executable)",proposed);if(target==null)return;target=Absolute(target);if(!File.Exists(target))throw new FileNotFoundException("Build the project with F6 first.",target);
        }
        debugger?.Dispose();debugger=new();debugger.Start(source,target,breakpoints);message="Debugger started.";
    }
    void ToggleBreakpoint()
    {
        if(Doc.FilePath==null){message="Save the document before setting breakpoints.";return;}
        if(!breakpoints.TryGetValue(Doc.FilePath,out var lines))breakpoints[Doc.FilePath]=lines=[];
        int line=Doc.Position.Row+1;if(!lines.Add(line))lines.Remove(line);debugger?.SetBreakpoints(Doc.FilePath,lines);message="Breakpoint toggled on line "+line;
    }
    void Complete()
    {
        List<Completion> completions;
        if(server!=null&&server.Language==Doc.Language&&Doc.FilePath!=null)completions=server.Complete(Doc);
        else completions=Languages.Complete(Doc).Select(w=>new Completion(w,w)).ToList();
        if(completions.Count==0){message="No completion. Install/connect a language server for project-aware results.";return;}
        Choose("Completion"+(server!=null?" — language server":" — local keywords and symbols"),completions.Select((c,i)=>new Choice(c.Label,i.ToString())).ToList(),value=>{var c=completions[int.Parse(value)];int start=c.Start??(Doc.Cursor-Doc.WordAtCursor().Length),len=c.Length??Doc.WordAtCursor().Length;Doc.Replace(start,len,c.Insert);Changed();});
    }
    void HandleFiles(KeyEvent k)
    {
        switch(k.Key){case "up":browser.Index=Math.Max(0,browser.Index-1);break;case "down":browser.Index=Math.Min(browser.Entries.Count-1,browser.Index+1);break;case "pageup":browser.Index=Math.Max(0,browser.Index-10);break;case "pagedown":browser.Index=Math.Min(browser.Entries.Count-1,browser.Index+10);break;case "backspace":var parent=Directory.GetParent(browser.Folder);if(parent!=null)browser.Load(parent.FullName);break;case "enter":OpenSelected("current");break;}
    }
    void OpenSelected(string target){string? path=browser.Selected;if(path==null)return;if(Directory.Exists(path)){browser.Load(path);return;}Open(path,target);}
    void FilePrompt()
    {
        var p=new Popup{Title="Open file — Enter: open | Ctrl+V: vertical | Ctrl+H: horizontal | Ctrl+T: tab",Prompt=true,Text=working+Path.DirectorySeparatorChar};
        p.Accept=(text,key)=>{string path=Absolute(text);if(Directory.Exists(path)){working=path;FilePrompt();return;}Open(path,key switch{"ctrl+v"=>"vertical","ctrl+h"=>"horizontal",_=>"tab"});};
        p.Changed=text=>{try{string dir=Path.GetDirectoryName(Absolute(text))??working;string prefix=Path.GetFileName(text);p.Items=Directory.EnumerateFileSystemEntries(dir).Where(f=>Path.GetFileName(f).StartsWith(prefix,StringComparison.OrdinalIgnoreCase)).Take(100).Select(f=>new Choice(f+(Directory.Exists(f)?Path.DirectorySeparatorChar:""),f)).ToList();}catch(IOException ex){ErrorLog.Write(ex,"File path suggestions");p.Items=[];}};
        popup=p;p.Changed(p.Text);
    }
    IEnumerable<string> InstalledFonts()
    {
        var fonts=new HashSet<string>{"FiraCode Nerd Font Mono"};
        if(OperatingSystem.IsWindows()) {foreach(var dir in new[]{Environment.GetFolderPath(Environment.SpecialFolder.Fonts),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","Windows","Fonts")})if(Directory.Exists(dir))foreach(var f in Directory.EnumerateFiles(dir).Where(p=>p.Contains("Nerd",StringComparison.OrdinalIgnoreCase)))fonts.Add(Path.GetFileNameWithoutExtension(f));}
        else if(BuildTools.Which("fc-list")!=null){using var p=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("fc-list",": family"){RedirectStandardOutput=true})!;foreach(var line in p.StandardOutput.ReadToEnd().Split('\n').Where(s=>s.Contains("Nerd")))foreach(var name in line.Split(','))fonts.Add(name.Trim());p.WaitForExit();}
        return fonts.Order();
    }
    void Colour(string title,string current,Action<string> apply)
    {
        var colours=new[]{"#d8dee9","#ffffff","#ff7777","#ffcc66","#82d4b3","#80c9ff","#d9a9ef","#151c28","#000000"};
        Prompt(title+" — hex #RRGGBB (Tab cycles presets)",current,(text,_)=>{if(!Regex.IsMatch(text,"^#[a-fA-F0-9]{6}$"))throw new FormatException("Use #RRGGBB, e.g. #ffcc66.");apply(text);});
        popup!.Items=colours.Select(c=>new Choice(c,c)).ToList();
    }
    void EditTags()
    {
        var doc=Doc;
        Prompt("Tags — comma-separated",string.Join(", ",DocumentTags.Read(doc)),(text,_)=>{DocumentTags.Set(doc,DocumentTags.Normalize(text));Changed();});
    }
    void SearchTags()
    {
        if(mode=="Text"){FocusBar("search");return;}
        Prompt("Search document tags","",(text,_)=>Choose("Files tagged "+text,DocumentTags.Find(working,text).Select(path=>new Choice(Path.GetRelativePath(working,path),path)).ToList(),path=>Open(path,"tab")));
    }
    void CheckSpelling()
    {
        var words=spell.Unknown(Doc.Text).ToList();Choose("Spelling — English dictionary",words.Select((w,i)=>new Choice(w.Word,i.ToString(),"At character "+(w.Index+1)+" — Enter for suggestions / Add to dictionary")).ToList(),value=>
        {
            var w=words[int.Parse(value)];Doc.Anchor=w.Index;Doc.Cursor=w.Index+w.Word.Length;
            var items=spell.Suggest(w.Word).Select(s=>new Choice(s,s)).ToList();items.Add(new("Add to personal dictionary","__add"));Choose("Replace "+w.Word,items,word=>{if(word=="__add")spell.Add(w.Word);else{Doc.Replace(w.Index,w.Word.Length,word);Changed();}});
        });
    }
    void Preview()
    {
        ShowText("Markdown preview — font family/point size depend on your terminal",Doc.Text);popup!.Title="Markdown preview";previewPopup=popup;
    }
    Popup? previewPopup;
    void ShowManual()
    {
        var languages= new[]{Doc.Language,"tailwind","codeigniter","next","nuxt"}.Distinct().Select(id=>new Choice(id,id)).ToList();
        string path=Path.Combine(AppContext.BaseDirectory,"manuals",Doc.Language+".md");
        if(File.Exists(path))ShowText(Languages.Get(Doc.Language).Name+" — offline guide",File.ReadAllText(path)+"\n\nRelated guides: use Help → Language Manual on React/Vue/PHP files; all guides are in the manuals folder.\n\n"+string.Join("\n\n",new[]{"tailwind","codeigniter","next","nuxt"}.Where(id=>Doc.Language switch{"react"=>id is "next" or "tailwind","vue"=>id is "nuxt" or "tailwind","php"=>id=="codeigniter","html" or "css"=>id=="tailwind",_=>false}).Select(id=>File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"manuals",id+".md")))));
        else ShowText("Offline guide","No language-specific manual for this extension. Use F1 for editor help.");
    }
    const string VimHelp="VIM subset: i/a/I/A insert; h/j/k/l move; w/b word; 0/$ line; gg/G document; o/O new line; v visual; dd delete line; yy copy line; y/d visual copy/cut; p paste; u undo; Ctrl+R redo; / find; n next; :w :q :q! :wq :qa :e PATH :split :vsplit :bn :bp :LINE. Esc returns to Normal. Other Vim features/macros/plugins are not implemented.";
    void Help()
    {
        string text;
        if(Doc.FilePath==Settings.ConfigPath)text=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"assets","config-help.md"));
        else if(TagStore.IsRegistryPath(Doc.FilePath))text=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"assets","tags-help.md"));
        else text="RudyEdit 2.0.5\n\nAlt+F/E/M/T/V/B/H menus • F10 menu • F8 focus file manager • Esc return to editor\nShift+arrows select • Ctrl+S save • Ctrl+Shift+W close tab • Ctrl+W close panel • Ctrl+Q quit\nCtrl+Alt+C config folder • Help → Error Log views persistent errors\nCtrl+Shift+P command palette • F2 Code/VIM • F3 Text/Development\nText bars: Ctrl+Shift+T toggle tag bar • Ctrl+Alt+M formatting • Ctrl+Alt+T add tags • Ctrl+Alt+G search tags. Enter applies; Esc returns.\nTags are saved in Tags.xml; documents contain a hidden identifier only. Back up Tags.xml with documents.\nText formatting edits Markdown/HTML source; Ctrl+Shift+M previews it.\nGraphic icons: select FiraCode Nerd Font Mono in the terminal. Help → Nerd Font & Icons has setup and a legend; View → Toggle Graphic Icons switches plain labels.\n\n"+string.Join("\n\n",Commands.All.Select(c=>$"{c.Name} [{c.Id}] — {c.Shortcut}\n{c.Description}\nExample 1: {c.Example}\nExample 2: {(c.Example2.Length>0?c.Example2:"Choose "+c.Name+" in the command palette.")}"))+"\n\n"+VimHelp;
        if(Doc.FilePath==Settings.KeysPath)text="KEYBINDING XML\nUse an existing command Name or id from the list below. Context: Global, Panel, Tab, File Manager, Text, Development. Keys: Ctrl+K, P, Right Arrow means three sequential keys. Save reloads valid bindings; a parse failure retains the last working map. More specific contexts win. All actions with nonempty default shortcuts are also included unless overridden by the same global sequence.\n\nExample:\n<Binding><Name>save</Name><Keys>Ctrl+k, s</Keys><Context>Global</Context><Description>Save file</Description></Binding>\n\n"+text;
        ShowText("Context help — type to search",text);
    }
    string IconLabel(UiIcon icon,string label)=>settings.UseNerdIcons?icon.Glyph+" "+label:label;
    void Palette()=>Choose("Commands — "+mode,Commands.All.Where(c=>c.Menu.Length>0&&(mode=="Development"||c.Menu!="Build")).DistinctBy(c=>c.Id).Select(c=>new Choice(IconLabel(UiIcons.Command(c.Id),c.Name+"  "+c.Shortcut),c.Id,c.Description+"\nExample: "+c.Example)).ToList(),Execute);
    IEnumerable<string> MainMenus=>new[]{"File","Edit","Mode","Text","View"}.Concat(mode=="Development"?["Build"]:Array.Empty<string>()).Concat(["Help"]);
    void ShowMenu()=>Choose("RudyEdit menu",MainMenus.Select(m=>new Choice(IconLabel(UiIcons.Menu(m),m+"  Alt+"+m[0]),m)).ToList(),OpenMenu);
    void OpenMenu(string menu)=>Choose(menu,Commands.All.Where(c=>c.Menu==menu).Select(c=>new Choice(IconLabel(UiIcons.Command(c.Id),c.Name+"  "+c.Shortcut),c.Id,c.Description+"\nExample: "+c.Example)).ToList(),Execute);
}
