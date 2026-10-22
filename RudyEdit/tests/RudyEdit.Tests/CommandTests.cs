using RudyEdit;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// Command tests use real keybinding dispatch and the editor's event loop. Reflection
// observes state and prepares fixtures; it never calls Execute/Handle/Save directly.
static class CommandTests
{
    public static HashSet<string> Covered {get;}=[];
    static readonly string[] ids=Commands.All.Select(c=>c.Id).Distinct().ToArray();
    static object? Get(EditorApp app,string name)=>typeof(EditorApp).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(app)??typeof(EditorApp).GetProperty(name,BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(app);
    static void Set(EditorApp app,string name,object? value)=>typeof(EditorApp).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(app,value);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    sealed class Script:ITerminal
    {
        readonly Queue<Func<(bool,KeyEvent?)>> steps=[];
        public int Width=>120;public int Height=>36;public string Screen="";
        public void Dispose(){}public void Invalidate(){}
        public void Draw(Screen s,int x=-1,int y=-1)=>Screen=Regex.Replace(string.Join('\n',s.Rows()),"\x1b\\[[0-9;]*m","");
        public KeyEvent? ReadKey(){if(steps.Count==0)throw new Exception("Script exhausted\n"+Screen);var (done,key)=steps.Peek()();if(done)steps.Dequeue();return key;}
        public void Key(string key,string text="")=>steps.Enqueue(()=>(true,new(key,text)));
        public void Do(Action action)=>steps.Enqueue(()=>{action();return(true,null);});
        public void Wait(Func<bool> condition,string label)
        {
            long started=0;steps.Enqueue(()=>{if(started==0)started=Environment.TickCount64;if(condition())return(true,null);if(Environment.TickCount64-started>15000)throw new Exception("Timed out: "+label+"\n"+Screen);return(false,null);});
        }
        public void Command(string id){int i=Array.IndexOf(ids,id);Check(i>=0,"Unknown test command "+id);Key("ctrl+alt+f12");Key(((char)('a'+i/26)).ToString());Key(((char)('a'+i%26)).ToString());}
        public void Text(string value)=>Key("text",value);
        public void Answer(string value){Key("ctrl+a");Text(value);Key("enter");}
    }
    sealed class Context
    {
        public readonly Script T=new();public EditorApp App=null!;
        public string Folder="",File="",Launched="";public Action? After;
        public Document D=>(Document)Get(App,"Doc")!;
        public Pane Active=>(Pane)Get(App,"active")!;
        public Layout Layout=>(Layout)Get(App,"layout")!;
        public FileBrowser Browser=>(FileBrowser)Get(App,"browser")!;
        public Popup? Popup=>Get(App,"popup") as Popup;
        public T State<T>(string name)=>(T)Get(App,name)!;
        public void SetState(string name,object value)=>Set(App,name,value);
        public void Assert(Func<bool> ok,string detail)=>T.Do(()=>Check(ok(),detail));
        public void Select()=>T.Do(()=>{D.Anchor=0;D.Cursor=5;});
        public void Command(string id)=>T.Command(id);
        public void PopupIs(string title)=>Assert(()=>Popup?.Title.Contains(title)==true,"Expected popup "+title);
        public string NewFile(string name,string text="hello") {string p=Path.Combine(Folder,name);System.IO.File.WriteAllText(p,text);return p;}
        public void SelectFile(string path)=>T.Do(()=>{Browser.Refresh();Browser.Index=Browser.Entries.IndexOf(path);Check(Browser.Index>=0,"File fixture absent");});
        public void Escape()=>T.Key("esc");
        public void Wait(Func<bool> ok,string label)=>T.Wait(ok,label);
    }
    public static void Run(Action<string,Action> test,string root)
    {
        var cases=new Dictionary<string,Action<Context>>();
        void Case(string id,Action<Context> run)=>cases.Add(id,run);
        Case("new",c=>{c.Command("new");c.Assert(()=>c.Active.Tabs.Count==2&&c.D.FilePath==null,"New tab was not created");});
        Case("open",c=>{string p=c.NewFile("opened.md","opened fixture");c.Command("open");c.T.Answer(p);c.Assert(()=>c.D.FilePath==p&&c.D.Text=="opened fixture","Open lost content/path");});
        Case("folder",c=>{string p=Path.Combine(c.Folder,"child");Directory.CreateDirectory(p);c.Command("folder");c.T.Answer(p);c.Assert(()=>c.Browser.Folder==p&&c.State<bool>("filesFocus"),"Folder did not load");});
        Case("save",c=>{c.T.Do(()=>c.D.Insert("saved"));c.Command("save");c.Assert(()=>!c.D.Dirty&&System.IO.File.ReadAllText(c.File).StartsWith("saved"),"Save did not persist edit");});
        Case("save-all",c=>{Document? second=null;c.T.Do(()=>{c.D.Insert("first");second=Document.Open(c.NewFile("second.md"));second.Insert("second");c.Active.Tabs.Add(second);});c.Command("save-all");c.Assert(()=>!c.D.Dirty&&!second!.Dirty&&System.IO.File.ReadAllText(second.FilePath!).StartsWith("second"),"Save All skipped a tab");});
        Case("save-as",c=>{string p=Path.Combine(c.Folder,"copy.md");c.Command("save-as");c.T.Answer(p);c.Assert(()=>c.D.FilePath==p&&System.IO.File.ReadAllText(p)=="hello world\nsecond line","Save As failed");});
        Case("close-tab",c=>{c.T.Do(()=>{c.Active.Tabs.Add(Document.Open(c.NewFile("close.md")));c.Active.Selected=1;});c.Command("close-tab");c.Assert(()=>c.Active.Tabs.Count==1&&c.D.FilePath==c.File,"Close Tab retained tab");});
        Case("close-other-tabs",c=>{c.T.Do(()=>c.Active.Tabs.Add(Document.Open(c.NewFile("close.md"))));c.Command("close-other-tabs");c.Assert(()=>c.Active.Tabs.Count==1&&c.D.FilePath==c.File,"Close Other Tabs changed current tab");});
        Case("quit",c=>{c.Command("quit");c.After=()=>Check(c.State<bool>("quit"),"Quit did not stop event loop");});
        foreach(string id in new[]{"keys-code","keys-vim","toggle-keys"})Case(id,c=>{c.T.Do(()=>c.SetState("keysMode",id=="keys-code"?"VIM":"Code"));c.Command(id);c.Assert(()=>c.State<string>("keysMode")== (id=="keys-code"?"Code":"VIM"),"Keymap command did not switch");});
        foreach(string id in new[]{"text-mode","dev-mode","toggle-mode"})Case(id,c=>{c.T.Do(()=>c.SetState("mode",id=="text-mode"?"Development":"Text"));c.Command(id);c.Assert(()=>c.State<string>("mode")== (id=="text-mode"?"Text":"Development"),"Mode command did not switch");});
        foreach(var (id,left,right) in new[]{("bold","**","**"),("italic","*","*"),("underline","<u>","</u>")})Case(id,c=>{c.Select();c.Command(id);c.Assert(()=>c.D.Text.StartsWith(left+"hello"+right),"Formatting did not wrap selection");});
        Case("font",c=>{c.Select();c.Command("font");c.T.Text(UiIcons.FontFamily);c.T.Key("enter");c.Assert(()=>c.D.Text.Contains("font-family:'FiraCode Nerd Font Mono'")&&c.D.Text.Contains("hello</span>"),"Font markup missing");});
        Case("font-size",c=>{c.Select();c.Command("font-size");c.T.Answer("18");c.Assert(()=>c.D.Text.StartsWith("<span style=\"font-size:18pt\">hello</span>"),"Point size markup missing");});
        foreach(string id in new[]{"foreground","background"})Case(id,c=>{c.Select();c.Command(id);c.T.Answer("#ff7777");c.Assert(()=>c.State<string>(id=="foreground"?"foreground":"background")=="#ff7777"&&(id=="background"||c.D.Text.Contains("color:#ff7777")),"Color was not applied");});
        foreach(var (id,prefix) in new[]{("bullets","- "),("ordered","1. ")})Case(id,c=>{c.Command(id);c.Assert(()=>c.D.Text.StartsWith(prefix+"hello"),"List prefix missing");});
        Case("toggle-tag-bar",c=>{c.Command("toggle-tag-bar");c.Assert(()=>!c.State<bool>("showTagBar"),"Tag bar stayed visible");});
        Case("tag-entry",c=>{c.Command("tag-entry");c.T.Text("work, notes");c.T.Key("enter");c.Assert(()=>DocumentTags.Read(c.D).SequenceEqual(new[]{"work","notes"}),"Tag entry did not update current document");});
        Case("format-bar",c=>{c.Command("format-bar");c.T.Key("right");c.T.Key("enter");c.PopupIs("Font size");c.T.Text("18 pt");c.T.Key("enter");c.Assert(()=>c.D.Text.Contains("font-size:18pt"),"Format bar selector failed");});
        Case("tags",c=>{c.Command("tags");c.T.Answer("work, WORK, project");c.Assert(()=>DocumentTags.Read(c.D).SequenceEqual(new[]{"work","project"}),"Tag editor normalization failed");});
        Case("search-tags",c=>{c.T.Do(()=>{DocumentTags.Set(c.D,["work"]);c.D.Save();});c.Command("search-tags");c.T.Text("work");c.T.Key("enter");c.Assert(()=>c.Browser.TagFilter=="work"&&c.Browser.Entries.Contains(c.File),"Tag search omitted saved file");});
        Case("spell",c=>{c.T.Do(()=>{c.D.Anchor=0;c.D.Cursor=c.D.Text.Length;c.D.Insert("helllo");});c.Command("spell");c.T.Text("helllo");c.T.Key("enter");c.PopupIs("Replace helllo");c.T.Text("hello");c.T.Key("enter");c.Assert(()=>c.D.Text=="hello","Spelling replacement failed");});
        Case("toggle-spell",c=>{c.Command("toggle-spell");c.Assert(()=>!c.State<Settings>("settings").SpellCheck,"Auto spelling did not toggle");});
        Case("preview",c=>{c.Command("preview");c.PopupIs("Markdown preview");c.Assert(()=>c.Popup!.Content==c.D.Text,"Preview lost document text");});
        Case("undo",c=>{c.T.Do(()=>c.D.Insert("undo"));c.Command("undo");c.Assert(()=>c.D.Text=="hello world\nsecond line","Undo failed");});
        Case("redo",c=>{c.T.Do(()=>{c.D.Insert("redo");c.D.Undo();});c.Command("redo");c.Assert(()=>c.D.Text.StartsWith("redohello"),"Redo failed");});
        Case("copy",c=>{c.Select();c.Command("copy");c.Assert(()=>c.State<string>("clipboard")=="hello"&&!c.D.Dirty,"Copy altered document or wrong clipboard");});
        Case("cut",c=>{c.Select();c.Command("cut");c.Assert(()=>c.State<string>("clipboard")=="hello"&&c.D.Text.StartsWith(" world"),"Cut failed");});
        Case("paste",c=>{c.T.Do(()=>c.SetState("clipboard","pasted"));c.Command("paste");c.Assert(()=>c.D.Text.StartsWith("pastedhello"),"Paste failed");});
        Case("select-all",c=>{c.Command("select-all");c.Assert(()=>c.D.Selection.Length==c.D.Text.Length,"Select All incomplete");});
        Case("find",c=>{c.Command("find");c.T.Answer("world");c.Assert(()=>c.D.Selection==new Selection(6,5),"Find failed to select match");});
        Case("find-next",c=>{c.T.Do(()=>c.SetState("find","second"));c.Command("find-next");c.Assert(()=>c.D.Selection==new Selection(12,6),"Find Next failed");});
        Case("replace",c=>{c.Command("replace");c.T.Answer("hello");c.T.Answer("goodbye");c.T.Key("down");c.T.Key("enter");c.Assert(()=>c.D.Text.StartsWith("goodbye world"),"Confirmed replace did not apply");});
        Case("goto",c=>{c.Command("goto");c.T.Answer("2");c.Assert(()=>c.D.Position==(1,0),"Go To Line wrong position");});
        Case("complete",c=>{c.T.Do(()=>{c.SetState("mode","Development");c.Active.Tabs[0]=Document.Open(c.NewFile("complete.py","pri"));c.D.Cursor=3;});c.Command("complete");c.T.Text("print");c.T.Key("enter");c.Assert(()=>c.D.Text=="print","Accepted completion failed");});
        foreach(string id in new[]{"build","run"})Case(id,c=>{c.T.Do(()=>c.Active.Tabs[0]=Document.Open(c.NewFile("execute.py","print('release-run-marker')\n")));c.Command(id);c.Wait(()=>!c.State<ProcessRunner>("runner").Active,"Python "+id);c.Assert(()=>c.State<List<string>>("output").Contains("[exit 0]")&&(id=="build"||c.State<List<string>>("output").Contains("release-run-marker")),"Build/run failed");});
        void DebugStart(Context c)
        {
            c.T.Do(()=>{c.Active.Tabs[0]=Document.Open(c.NewFile("debug.py","def add(value):\n    result=value+2\n    return result\nvalue=1\nvalue=add(value)\nprint(value)\n"));c.SetState("mode","Development");});
            c.Command("debug");c.Wait(()=>c.State<int>("debugLine")>0,"debugger initial pause");
        }
        Case("debug",c=>{DebugStart(c);c.Assert(()=>c.State<DebuggerSession>("debugger").Active&&c.State<string>("message").StartsWith("Paused"),"Debugger did not pause");});
        foreach(var (id,line) in new[]{("step-over",4),("step-in",2),("step-out",6)})Case(id,c=>
        {
            DebugStart(c);
            c.Command("step-over");c.Wait(()=>c.State<int>("debugLine")==4,"step to assignment");
            if(id!="step-over") {c.Command("step-over");c.Wait(()=>c.State<int>("debugLine")==5,"step to function call");c.Command("step-in");c.Wait(()=>c.State<int>("debugLine")==2,"enter function");}
            if(id=="step-in"){c.Assert(()=>c.State<int>("debugLine")==2,"Step Into skipped function");}
            else if(id=="step-over"){c.Assert(()=>c.State<int>("debugLine")==line,"Step Over wrong line");}
            else{c.Command(id);c.Wait(()=>c.State<int>("debugLine")==line,"step out to caller");c.Assert(()=>c.State<int>("debugLine")==line,"Step Out failed");}
        });
        Case("breakpoint",c=>{c.Command("breakpoint");c.Assert(()=>c.State<Dictionary<string,HashSet<int>>>("breakpoints")[c.File].Contains(1),"Breakpoint missing");c.Command("breakpoint");c.Assert(()=>!c.State<Dictionary<string,HashSet<int>>>("breakpoints")[c.File].Contains(1),"Breakpoint did not remove");});
        Case("variables",c=>{DebugStart(c);c.Command("step-over");c.Wait(()=>c.State<int>("debugLine")==4,"step debugger");c.Command("step-over");c.Wait(()=>c.State<int>("debugLine")==5,"assignment completed");c.Command("variables");c.Wait(()=>c.Popup?.Title=="Debug variables","variables response");c.Assert(()=>c.Popup!.Content!.Contains("value"),"Local variable missing");});
        Case("stop",c=>{DebugStart(c);c.Command("stop");c.Assert(()=>Get(c.App,"debugger")==null&&c.State<string>("message")=="Stopped.","Stop retained debugger");});
        Case("diagnostics",c=>{c.T.Do(()=>c.State<List<Diagnostic>>("diagnostics").Add(new(c.File,2,3,"test diagnostic")));c.Command("diagnostics");c.T.Key("enter");c.Assert(()=>c.D.Position==(1,2),"Diagnostic did not jump to source");});
        Case("lsp",c=>{c.T.Do(()=>{c.Active.Tabs[0]=Document.Open(c.NewFile("language.py","😀 pri"));c.D.Cursor=c.D.Text.Length;c.SetState("mode","Development");string fixture=Path.Combine(AppContext.BaseDirectory,"tests","mock_lsp.py");if(!System.IO.File.Exists(fixture))fixture=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../mock_lsp.py"));System.IO.File.WriteAllText(Path.Combine(Settings.ConfigDirectory,"languages.json"),System.Text.Json.JsonSerializer.Serialize(new Dictionary<string,object>{{"python",new{command=BuildTools.Which(BuildTools.Python)!,args=new[]{fixture}}}}));});c.Command("lsp");c.Assert(()=>Get(c.App,"server")!=null&&c.State<string>("message").Contains("connected"),"LSP failed to connect");c.Command("complete");c.T.Text("print");c.T.Key("enter");c.Assert(()=>c.D.Text=="😀 print","LSP ranged completion failed");});
        Case("terminal",c=>{c.Command("terminal");c.T.Text("echo release-shell-marker");c.T.Key("enter");c.Wait(()=>c.State<List<string>>("output").Any(s=>s.Trim()=="release-shell-marker"),"shell command output");c.Assert(()=>c.State<bool>("showConsole")&&c.State<bool>("consoleFocus"),"Console did not focus");});
        Case("console",c=>{c.Command("console");c.Assert(()=>c.State<bool>("showConsole"),"Console did not show");c.Command("console");c.Assert(()=>!c.State<bool>("showConsole"),"Console did not hide");});
        Case("files",c=>{c.Command("files");c.Assert(()=>c.State<bool>("showFiles")&&c.State<bool>("filesFocus"),"File Manager did not show/focus");c.Command("files");c.Assert(()=>!c.State<bool>("showFiles"),"File Manager did not hide");});
        foreach(string id in new[]{"vertical","horizontal"})Case(id,c=>{c.Command(id);c.Assert(()=>c.Layout.Panes.Count()==2,"Split did not add pane");c.T.Do(()=>{var r=c.Layout.Arrange(new(0,0,100,40)).Values.ToList();Check(id=="vertical"?r.All(x=>x.W==50):r.All(x=>x.H==20),"Split orientation incorrect");});});
        Case("close-pane",c=>{c.Command("vertical");c.Command("close-pane");c.Assert(()=>c.Layout.Panes.Count()==1,"Close Panel retained pane");});
        Case("close-other-panes",c=>{c.Command("vertical");c.Command("horizontal");c.Command("close-other-panes");c.Assert(()=>c.Layout.Panes.Count()==1,"Other panes remained");});
        foreach(string id in new[]{"next-pane","previous-pane"})Case(id,c=>{Pane? before=null;c.Command("vertical");c.T.Do(()=>before=c.Active);c.Command(id);c.Assert(()=>c.Active!=before,"Pane navigation did not focus another pane");});
        foreach(string id in new[]{"next-tab","previous-tab"})Case(id,c=>{c.T.Do(()=>{c.Active.Tabs.Add(Document.Open(c.NewFile("other.md")));});c.Command(id);c.Assert(()=>c.Active.Selected==1&&c.D.Name=="other.md","Tab navigation failed");});
        Case("toggle-icons",c=>{c.Command("toggle-icons");c.Assert(()=>!c.State<Settings>("settings").UseNerdIcons,"Icons did not toggle");});
        foreach(var (id,title,content) in new[]{("font-icons","Nerd Font & Icons","ICON LEGEND"),("help","Context help","Ctrl+Q"),("shortcuts","Keybindings","Save"),("error-log","Error log","Error log opened"),("manual","offline guide","Markdown")})Case(id,c=>{c.Command(id);c.PopupIs(title);c.Assert(()=>c.Popup!.Content?.Contains(content,StringComparison.OrdinalIgnoreCase)==true,"Help content missing: "+id);});
        foreach(var (id,file) in new[]{("edit-config","Config.cs"),("edit-keys","KeyboardShortcuts.xml")})Case(id,c=>{c.Command(id);c.Assert(()=>c.D.FilePath==Path.Combine(Settings.ConfigDirectory,file),"Settings file did not open");});
        Case("config-folder",c=>{c.Command("config-folder");c.Assert(()=>c.Launched==Settings.ConfigDirectory&&c.Browser.Folder==Settings.ConfigDirectory,"Config folder/opener incorrect");});
        Case("palette",c=>{c.Command("palette");c.T.Text("New File");c.T.Key("enter");c.Assert(()=>c.D.FilePath==null&&c.Active.Tabs.Count==2,"Palette did not execute selected command");});
        foreach(string id in new[]{"fm-open","fm-tab"})Case(id,c=>{string p=c.NewFile("browse.md","browsed");c.SelectFile(p);c.Command(id);c.Assert(()=>c.D.FilePath==p&&c.D.Text=="browsed","File Manager open failed");});
        Case("fm-find",c=>{c.NewFile("unique.md");c.Command("fm-find");c.T.Answer("unique");c.Assert(()=>c.Browser.Entries.Any(p=>p.EndsWith("unique.md"))&&!c.Browser.Entries.Contains(c.File),"Filename filter failed");});
        foreach(var (id,name) in new[]{("fm-new","created.txt"),("fm-folder","created-folder")})Case(id,c=>{c.Command(id);c.T.Answer(name);c.Assert(()=>id=="fm-new"?System.IO.File.Exists(Path.Combine(c.Folder,name)):Directory.Exists(Path.Combine(c.Folder,name)),"File Manager create failed");});
        foreach(string id in new[]{"fm-copy","fm-move","fm-paste"})Case(id,c=>{string source=c.NewFile("transfer.txt","transfer data"),dest=Path.Combine(c.Folder,"destination");Directory.CreateDirectory(dest);c.SelectFile(source);c.Command(id=="fm-move"?"fm-move":"fm-copy");c.T.Do(()=>c.Browser.Load(dest));c.Command("fm-paste");c.Assert(()=>System.IO.File.ReadAllText(Path.Combine(dest,"transfer.txt"))=="transfer data"&&System.IO.File.Exists(source)==(id!="fm-move"),"File Manager transfer failed");});

        foreach(var item in cases)
        foreach(bool vim in new[]{false,true})
        test("[command:"+item.Key+"] verified outcome via keybinding / "+(vim?"VIM":"Code"),()=>
        {
            string? previous=Environment.GetEnvironmentVariable("RUDYEDIT_CONFIG_HOME");
            string folder=Path.Combine(root,"commands",item.Key+"-"+vim),config=Path.Combine(folder,"config");Directory.CreateDirectory(folder);
            Environment.SetEnvironmentVariable("RUDYEDIT_CONFIG_HOME",config);
            try
            {
                Settings.EnsureDefaults();System.IO.File.WriteAllText(Settings.ConfigPath,"DefaultKeysMode = "+(vim?"VIM":"Code")+";\n");
                var xml=new XDocument(new XElement("KeyboardShortcuts",ids.Select((id,i)=>new XElement("Binding",new XElement("Name",id),new XElement("Keys","Ctrl+Alt+F12, "+(char)('a'+i/26)+", "+(char)('a'+i%26)),new XElement("Context","Global")))));xml.Save(Settings.KeysPath);
                var c=new Context{Folder=folder};c.File=c.NewFile("document.md","hello world\nsecond line");item.Value(c);
                if(item.Key!="quit")c.T.Do(()=>Set(c.App,"quit",true));
                using var app=new EditorApp(folder,c.File,false,c.T,path=>c.Launched=path);c.App=app;app.Run();c.After?.Invoke();Covered.Add(item.Key);
            }
            finally{Environment.SetEnvironmentVariable("RUDYEDIT_CONFIG_HOME",previous);}
        });
        test("Every command in the executable catalog has a verified outcome test",()=>
        {var missing=ids.Except(cases.Keys).ToArray();Check(missing.Length==0,"Uncovered functions: "+string.Join(", ",missing));Check(ids.All(Covered.Contains),"Failed/unexecuted functions: "+string.Join(", ",ids.Except(Covered)));});
    }
}
