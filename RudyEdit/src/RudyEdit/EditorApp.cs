using System.Text;
using System.Text.RegularExpressions;
using System.Net;
namespace RudyEdit;
public sealed record Choice(string Label,string Value,string Detail="");
public sealed class Popup
{
    public string Title="",Text="";public List<Choice> Items=[];public string? Content;public bool Prompt,Dropdown;public int Index,Scroll;public Action<string,string>? Accept;public Action<string>? Changed;
    public List<Choice> Filtered=>Items.Where(x=>x.Label.Contains(Text,StringComparison.OrdinalIgnoreCase)||x.Detail.Contains(Text,StringComparison.OrdinalIgnoreCase)).ToList();
}
public sealed partial class EditorApp:IDisposable
{
    readonly Action<string> folderLauncher;
    readonly ITerminal terminal;Settings settings;List<Binding> bindings;
    readonly Layout layout=new();Pane active;readonly FileBrowser browser;readonly SpellChecker spell;
    readonly ProcessRunner runner=new();DebuggerSession? debugger;LanguageServer? server;
    readonly List<string> output=[];readonly List<Diagnostic> diagnostics=[];
    readonly Dictionary<string,HashSet<int>> breakpoints=new();
    string? debugFile;int debugLine;string working,mode,keysMode,vim="NORMAL",vimPending="",clipboard="",find="",commandLine="",chord="",fileClipboard="";
    bool showFiles,showConsole,filesFocus,consoleFocus,quit,moveFile,vimCommand,quitting;
    Popup? popup;DateTime chordTime,lastChange;string message="Ready — Alt+letter menu • Ctrl+Q quit • F1 help",lastServerText="";
    int consoleScroll;string foreground,background;string? buildThenRun;
    int width=100,height=30;
    Document Doc=>active.Document;
    public EditorApp(string folder,string? file,bool openFolder,ITerminal? terminal=null,Action<string>? folderLauncher=null)
    {
        this.folderLauncher=folderLauncher??ConfigFolder.Open;ErrorLog.Initialize();
        Settings.EnsureDefaults();settings=new();bindings=[];string? error=null;
        try{settings=Settings.Load(Settings.ConfigPath);}catch(Exception ex){ErrorLog.Write(ex,"Load Config.cs");error=ex.Message;}
        try{bindings=KeyBindings.Load(Settings.KeysPath);}catch(Exception ex){ErrorLog.Write(ex,"Load KeyboardShortcuts.xml");bindings=DefaultBindings();error=(error??"")+ex.Message;}
        showTagBar=settings.ShowTagBar;foreground=settings.Foreground;background=settings.Background;mode=settings.DefaultMode;keysMode=settings.DefaultKeysMode;
        showFiles=openFolder||settings.OpenFileManager;showConsole=mode=="Development"?settings.DevelopmentConsole:settings.TextConsole;
        working=folder;browser=new(folder);active=layout.Panes.First();spell=new();
        if(file!=null)Open(file,"current");
        this.terminal=terminal??new Terminal();runner.Exited+=code=>{if(buildThenRun!=null){var pending=buildThenRun;buildThenRun=null;if(code==0)pendingRun=pending;}};
        if(error!=null)ShowText("Configuration error — using safe defaults",error+"\nF10 → Help → Config or Keybindings to repair.");
    }
    string? pendingRun;
    static List<Binding> DefaultBindings()=>Commands.WithDefaultShortcuts.Select(c=>new Binding(c.Id,c.Shortcut.Split(',').Select(KeyBindings.Normalize).ToArray(),"Global",c.Description)).ToList();
    public void Run()
    {
        while(!quit)
        {
            Drain();if(pendingRun is{} path){pendingRun=null;if(!runner.Active)Try(()=>runner.Start(BuildTools.Plan(path,true)));}
            if(chord.Length>0&&(DateTime.UtcNow-chordTime).TotalMilliseconds>1500){message="Shortcut timed out: "+chord;chord="";}
            if(server!=null&&Doc.Language==server.Language&&Doc.Text!=lastServerText&&(DateTime.UtcNow-lastChange).TotalMilliseconds>650)
                Try(()=>{server.Sync(Doc);lastServerText=Doc.Text;});
            Draw();var key=terminal.ReadKey();if(key.HasValue){Try(()=>Dispatch(key.Value));}else Thread.Sleep(12);
        }
    }
    void Try(Action action){try{action();}catch(Exception ex){bool logged=ErrorLog.Write(ex,"Editor operation");message=ex.Message+(logged?"":" (error log unavailable)");output.Add("Error: "+ex.Message);if(output.Count>4000)output.RemoveRange(0,1000);}}
    void Drain()
    {
        while(runner.Output.TryDequeue(out var s))output.Add(s);
        while(runner.Diagnostics.TryDequeue(out var d))diagnostics.Add(d);
        if(server!=null){while(server.Output.TryDequeue(out var s))output.Add("[LSP] "+s);while(server.Diagnostics.TryDequeue(out var d)){if(d.Line==0)diagnostics.RemoveAll(x=>x.File==d.File);else diagnostics.Add(d);}}
        if(debugger!=null)while(debugger.Events.TryDequeue(out var e))
        {
            if(e.Kind=="stopped") {debugFile=e.File;debugLine=e.Line;if(e.File!=null&&File.Exists(e.File)){Open(e.File,"tab");Doc.SetPosition(Math.Max(0,e.Line-1),0);}message="Paused — F10 next • F11 into • Shift+F11 out • F5 continue";output.Add("[paused] "+e.File+":"+e.Line+" "+e.Text);}
            else if(e.Kind=="variables")ShowText("Debug variables",e.Text);
            else{output.Add(e.Text);if(e.Kind=="terminated"){debugFile=null;debugLine=0;message="Debug session ended.";}if(e.Kind=="input"){showConsole=consoleFocus=true;filesFocus=false;}}
        }
        if(output.Count>5000)output.RemoveRange(0,output.Count-4000);
    }
    // Unwind a synchronous dialog before opening a menu or starting quit, so its
    // suspended command cannot resume against a different dialog's selection.
    sealed class GlobalShortcut(string? menu) : Exception { public string? Menu { get; } = menu; }
    void Dispatch(KeyEvent k)
    {
        try { Handle(k); }
        catch(GlobalShortcut shortcut)
        {
            popup=null;barFocus="";chord=vimPending="";vimCommand=false;commandLine="";
            if(shortcut.Menu==null)Quit();else OpenMenu(shortcut.Menu);
        }
    }
    void Handle(KeyEvent k)
    {
        if(k.Mouse is {} mouse){HandleMouse(mouse);return;}
        draggingPane=null;
        active.FollowCursor=true;
        if(!k.Paste)
        {
            if(k.Key=="ctrl+q"){if(!quitting)throw new GlobalShortcut(null);return;}
            string? menu=k.Key switch {"alt+f"=>"File","alt+e"=>"Edit","alt+m"=>"Mode","alt+t"=>"Text","alt+v"=>"View","alt+b" when mode=="Development"=>"Build","alt+h"=>"Help",_=>null};
            if(menu!=null){if(!quitting)throw new GlobalShortcut(menu);return;}
        }
        if(popup!=null){HandlePopup(k);return;}
        if(HandleTextBars(k))return;
        if(keysMode=="VIM"&&vim=="NORMAL"&&k.Key=="ctrl+r"){Doc.Redo();Changed();return;}
        if(k.Key=="esc") {if(vimCommand){vimCommand=false;commandLine="";return;}filesFocus=consoleFocus=false;chord="";if(keysMode=="VIM"){vim="NORMAL";Doc.Anchor=null;vimPending="";}return;}
        if(k.Key=="f10"&&debugger?.Active!=true){ShowMenu();return;}
        if(k.Key=="f8"){showFiles=true;filesFocus=!filesFocus;consoleFocus=false;return;}
        if(k.Paste){if(consoleFocus)commandLine+=k.Text.Replace('\n',' ');else if(!filesFocus){Doc.Insert(k.Text);Changed();}return;}
        if(vimCommand){if(k.Key=="enter"){string cmd=commandLine;vimCommand=false;commandLine="";ExecuteVim(cmd);}else if(k.Key=="backspace"){if(commandLine.Length>0)commandLine=commandLine[..^1];}else if(k.Text.Length>0)commandLine+=k.Text;return;}
        if(MatchBinding(k.Key))return;
        if(consoleFocus){HandleConsole(k);return;}
        if(filesFocus){HandleFiles(k);return;}
        if(keysMode=="VIM"&&vim!="INSERT"&&VimKey(k))return;
        switch(k.Key)
        {
            case "left":Doc.Move(-1,0,keysMode=="VIM"&&vim=="VISUAL");break;case "right":Doc.Move(1,0,keysMode=="VIM"&&vim=="VISUAL");break;case "up":Doc.Move(0,-1,keysMode=="VIM"&&vim=="VISUAL");break;case "down":Doc.Move(0,1,keysMode=="VIM"&&vim=="VISUAL");break;
            case "shift+left":Doc.Move(-1,0,true);break;case "shift+right":Doc.Move(1,0,true);break;case "shift+up":Doc.Move(0,-1,true);break;case "shift+down":Doc.Move(0,1,true);break;
            case "ctrl+left":WordMove(-1,false);break;case "ctrl+right":WordMove(1,false);break;case "ctrl+shift+left":WordMove(-1,true);break;case "ctrl+shift+right":WordMove(1,true);break;
            case "home":Doc.Anchor=null;Doc.SetPosition(Doc.Position.Row,0);break;case "end":Doc.Anchor=null;Doc.SetPosition(Doc.Position.Row,int.MaxValue);break;
            case "ctrl+home":Doc.Anchor=null;Doc.Cursor=0;break;case "ctrl+end":Doc.Anchor=null;Doc.Cursor=Doc.Text.Length;break;
            case "shift+home":Doc.Anchor??=Doc.Cursor;Doc.SetPosition(Doc.Position.Row,0);break;case "shift+end":Doc.Anchor??=Doc.Cursor;Doc.SetPosition(Doc.Position.Row,int.MaxValue);break;
            case "pageup":Doc.Move(0,-Math.Max(1,height-8),false);break;case "pagedown":Doc.Move(0,Math.Max(1,height-8),false);break;
            case "backspace":Doc.Backspace();Changed();break;case "delete":Doc.Delete();Changed();break;
            case "enter":Newline();break;
            case "tab":Doc.Insert(settings.TabSpaces?new string(' ',settings.DefaultTab):"\t");Changed();break;
            default:if(k.Text.Length>0&&!k.Key.StartsWith("ctrl+")&&!k.Key.StartsWith("alt+")){Doc.Insert(k.Text);Changed();}break;
        }
    }
    void Changed(){lastChange=DateTime.UtcNow;consoleScroll=0;}
    bool MatchBinding(string key)
    {
        string context=filesFocus?"File Manager":mode;
        var candidates=bindings.Where(b=>b.Context=="Global"||b.Context==context||(!filesFocus&&(b.Context is "Panel" or "Tab"))).OrderBy(b=>b.Context=="Global"?1:0).ToList();
        string full=chord.Length==0?key:chord+","+key;var parts=full.Split(',');
        var exact=candidates.FirstOrDefault(b=>b.Keys.SequenceEqual(parts));
        if(exact!=null){chord="";Execute(exact.Command);return true;}
        if(candidates.Any(b=>b.Keys.Length>parts.Length&&b.Keys.Take(parts.Length).SequenceEqual(parts))){chord=full;chordTime=DateTime.UtcNow;message="Keys: "+full;return true;}
        if(chord.Length>0){message="No binding: "+full;chord="";return true;}return false;
    }
    void WordMove(int direction,bool select)
    {
        if(select)Doc.Anchor??=Doc.Cursor;else Doc.Anchor=null;
        int p=Doc.Cursor;bool Word(char c)=>char.IsLetterOrDigit(c)||c=='_';
        if(direction>0){while(p<Doc.Text.Length&&Word(Doc.Text[p]))p++;while(p<Doc.Text.Length&&!Word(Doc.Text[p]))p++;}
        else{while(p>0&&!Word(Doc.Text[p-1]))p--;while(p>0&&Word(Doc.Text[p-1]))p--;}Doc.Cursor=p;
    }
    void Newline()
    {
        var p=Doc.Position;string line=Doc.Lines[p.Row];string indent=Regex.Match(line,@"^\s*").Value;
        if(mode=="Development"&&line[..Math.Min(p.Column,line.Length)].TrimEnd().EndsWith('{'))indent+=settings.TabSpaces?new string(' ',settings.DefaultTab):"\t";
        if(mode=="Text") {var list=Regex.Match(line,@"^\s*(- |\* |\d+\. )");if(list.Success){var num=Regex.Match(list.Value,@"\d+");indent=list.Value;if(num.Success)indent=Regex.Replace(indent,@"\d+",(int.Parse(num.Value)+1).ToString());}}
        Doc.Insert("\n"+indent);Changed();
    }
    void HandleConsole(KeyEvent k)
    {
        if(k.Key=="ctrl+q"){Quit();return;}if(k.Key=="shift+f5"){Stop();return;}if(k.Key=="pageup"){consoleScroll+=5;return;}if(k.Key=="pagedown"){consoleScroll=Math.Max(0,consoleScroll-5);return;}
        if(k.Key=="enter") {string text=commandLine;commandLine="";consoleScroll=0;if(debugger?.Active==true)debugger.Input(text);else if(runner.Active)runner.Input(text);else if(text.Length>0)runner.Shell(text,working);}
        else if(k.Key=="backspace"){if(commandLine.Length>0)commandLine=commandLine[..^1];}else if(k.Text.Length>0)commandLine+=k.Text;
    }
    bool VimKey(KeyEvent k)
    {
        if(k.Key.StartsWith("ctrl+")||k.Key.StartsWith("alt+"))return false;
        string text=k.Text;
        if(k.Key is "left" or "right" or "up" or "down"){vimPending="";return false;}
        if(vimPending.Length>0){var pending=vimPending;vimPending="";if(pending=="d"&&text=="d"){CutLine();Changed();}else if(pending=="y"&&text=="y")clipboard=Doc.Lines[Doc.Position.Row]+"\n";else if(pending=="g"&&text=="g"){Doc.Cursor=0;Doc.Anchor=null;}return true;}
        switch(text)
        {
            case "i":vim="INSERT";Doc.Anchor=null;return true;case "a":Doc.Move(1,0,false);vim="INSERT";return true;
            case "I":Doc.SetPosition(Doc.Position.Row,0);vim="INSERT";return true;case "A":Doc.SetPosition(Doc.Position.Row,int.MaxValue);vim="INSERT";return true;
            case "h":Doc.Move(-1,0,vim=="VISUAL");return true;case "j":Doc.Move(0,1,vim=="VISUAL");return true;case "k":Doc.Move(0,-1,vim=="VISUAL");return true;case "l":Doc.Move(1,0,vim=="VISUAL");return true;
            case "w":WordMove(1,vim=="VISUAL");return true;case "b":WordMove(-1,vim=="VISUAL");return true;
            case "0":Doc.SetPosition(Doc.Position.Row,0);return true;case "$":Doc.SetPosition(Doc.Position.Row,int.MaxValue);return true;
            case "G":Doc.Cursor=Doc.Text.Length;return true;case "g":vimPending="g";return true;
            case "o":Doc.SetPosition(Doc.Position.Row,int.MaxValue);Newline();vim="INSERT";return true;
            case "O":Doc.SetPosition(Doc.Position.Row,0);Doc.Insert("\n");Doc.Move(0,-1,false);vim="INSERT";Changed();return true;
            case "v":vim=vim=="VISUAL"?"NORMAL":"VISUAL";Doc.Anchor=vim=="VISUAL"?Doc.Cursor:null;return true;
            case "d":if(vim=="VISUAL"&&Doc.Selection.Length>0){clipboard=SelectedText();Doc.Insert("");vim="NORMAL";Changed();}else vimPending="d";return true;
            case "y":if(vim=="VISUAL"){clipboard=SelectedText();vim="NORMAL";Doc.Anchor=null;}else vimPending="y";return true;
            case "p":Doc.Insert(clipboard);Changed();return true;case "x":Doc.Delete();Changed();return true;case "u":Doc.Undo();Changed();return true;
            case ":":vimCommand=true;commandLine="";return true;case "/":Execute("find");return true;case "n":Execute("find-next");return true;
        }
        return k.Text.Length>0||k.Key is "enter" or "backspace" or "delete" or "tab";
    }
    void ExecuteVim(string text)
    {
        text=text.Trim();if(int.TryParse(text,out int line)){Doc.SetPosition(line-1,0);return;}
        if(text.StartsWith("e ")){Open(Absolute(text[2..]),"current");return;}
        switch(text){case "w":Save(Doc,false);break;case "q":CloseTab();break;case "q!":CloseTab(true);break;case "wq":if(Save(Doc,false))CloseTab();break;case "qa":Quit();break;case "split":Split(false);break;case "vsplit":Split(true);break;case "bn":Tab(1);break;case "bp":Tab(-1);break;case "help":Help();break;default:message="Supported: :w :q :q! :wq :qa :e PATH :split :vsplit :bn :bp :LINE :help";break;}
    }
    void CutLine(){var p=Doc.Position;int start=Doc.Cursor-p.Column,length=Doc.Lines[p.Row].Length+(p.Row<Doc.Lines.Length-1?1:0);clipboard=Doc.Text.Substring(start,length);Doc.Replace(start,length,"");}
    string SelectedText()=>Doc.Selection.Length>0?Doc.Text.Substring(Doc.Selection.Start,Doc.Selection.Length):Doc.Lines[Doc.Position.Row]+"\n";
    string Absolute(string path)=>Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')),working);
    void Open(string path,string target)
    {
        path=Absolute(path);var existing=layout.Panes.SelectMany(p=>p.Tabs.Select(d=>(p,d))).FirstOrDefault(x=>x.d.FilePath==path);
        if(existing.d!=null&&target is not ("vertical" or "horizontal")){active=existing.p;active.Selected=active.Tabs.IndexOf(existing.d);return;}
        var doc=existing.d??Document.Open(path);
        if(target=="vertical"||target=="horizontal")active=layout.Split(active,target=="vertical");
        if(active.Document.FilePath==null&&!active.Document.Dirty)active.Tabs[active.Selected]=doc;
        else{active.Tabs.Add(doc);active.Selected=active.Tabs.Count-1;}
        filesFocus=consoleFocus=false;active.ScrollX=active.ScrollY=0;lastServerText="";message="Opened "+path;
    }
    void Split(bool vertical)
    {
        if(layout.Panes.Count()>=8)throw new IOException("Maximum of eight panes.");
        var doc=Doc;active=layout.Split(active,vertical);active.Tabs[0]=doc;filesFocus=consoleFocus=false;
    }
    void Tab(int delta){active.Selected=(active.Selected+delta+active.Tabs.Count)%active.Tabs.Count;active.ScrollX=active.ScrollY=0;lastServerText="";}
    void NextPane(int delta){var list=layout.Panes.ToList();active=list[(list.IndexOf(active)+delta+list.Count)%list.Count];filesFocus=consoleFocus=false;lastServerText="";}
    bool Save(Document d,bool saveAs)
    {
        string? path=d.FilePath;
        if(saveAs||path==null){path=PromptSync("Save As — Enter saves; Esc cancels",path??Path.Combine(working,d.Name));if(path==null)return false;path=Absolute(path);}
        try{d.Save(path);}catch(IOException ex) when(ex is not TagStorageException)
        {
            ErrorLog.Write(ex,"Save document "+path);
            if(File.Exists(path)&&Confirm(ex.Message+"\nOverwrite this file explicitly?",false))d.Save(path,true);else{message=ex.Message;return false;}
        }
        message="Saved "+d.FilePath;
        if(d.FilePath==Settings.ConfigPath){var next=Settings.Load(Settings.ConfigPath);settings=next;foreground=next.Foreground;background=next.Background;message="Saved and reloaded settings (default mode applies on next launch).";}
        if(d.FilePath==Settings.KeysPath){bindings=KeyBindings.Load(Settings.KeysPath);message="Saved and reloaded shortcuts.";}
        return true;
    }
    bool MayClose(Document d)
    {
        if(!d.Dirty)return true;
        string? answer=ChooseSync("Unsaved: "+d.Name,[new("Save","save"),new("Discard changes","discard"),new("Cancel","cancel")]);
        return answer=="discard"||(answer=="save"&&Save(d,false));
    }
    void CloseTab(bool force=false)
    {
        var d=Doc;bool other=layout.Panes.SelectMany(x=>x.Tabs).Count(x=>ReferenceEquals(x,d))>1;
        if(!force&&!other&&!MayClose(d))return;active.Tabs.RemoveAt(active.Selected);if(active.Tabs.Count==0)active.Tabs.Add(new());active.Selected=Math.Min(active.Selected,active.Tabs.Count-1);
    }
    void ClosePane(Pane pane)
    {
        foreach(var d in pane.Tabs.Distinct())if(!layout.Panes.Where(p=>p!=pane).SelectMany(p=>p.Tabs).Contains(d)&&!MayClose(d))return;
        if(layout.Panes.Count()==1){pane.Tabs.Clear();pane.Tabs.Add(new());pane.Selected=0;return;}layout.Remove(pane);active=layout.Panes.First();
    }
    void Quit()
    {
        if(quitting)return;
        quitting=true;
        try
        {
            foreach(var d in layout.Panes.SelectMany(p=>p.Tabs).Distinct())if(!MayClose(d))return;
            if((runner.Active||debugger?.Active==true)&&!Confirm("A program is running. Stop it and exit?",false))return;
            quit=true;
        }
        finally {quitting=false;}
    }
    public void Dispose()
    {
        try{server?.Dispose();}
        finally{try{debugger?.Dispose();}finally{try{runner.Dispose();}finally{terminal.Dispose();}}}
    }

}
