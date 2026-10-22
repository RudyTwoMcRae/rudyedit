using System.Diagnostics;
using System.Text;
using RudyEdit;

string? reportPath=null;
for(int i=0;i<args.Length;i++)if(args[i]=="--report"&&i+1<args.Length)reportPath=Path.GetFullPath(args[++i]);
if(args.Contains("--windows-input"))return WindowsNativeTests.Run(reportPath);
var results=new List<object>();
string Fixture(string name){var shipped=Path.Combine(AppContext.BaseDirectory,"tests",name);return File.Exists(shipped)?shipped:Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../",name));}

string root=Path.Combine(Path.GetTempPath(),"rudyedit-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
Environment.SetEnvironmentVariable("RUDYEDIT_CONFIG_HOME",Path.Combine(root,"config"));
int passed=0,failed=0;
void Test(string name,Action action)
{
    var watch=Stopwatch.StartNew();string status="PASS",error="";
    try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception ex){failed++;status="FAIL";error=ex.ToString();Console.WriteLine("FAIL "+name+": "+ex);}
    results.Add(new{name,status,error,duration_ms=watch.ElapsedMilliseconds});
}
void Eq<T>(T actual,T expected){if(!Equals(actual,expected))throw new Exception($"Expected {expected}, got {actual}");}
void True(bool value){if(!value)throw new Exception("Condition failed.");}
void Throws(Action action){try{action();}catch{ return;}throw new Exception("Expected an exception.");}
string FileAt(string name,string text){string path=Path.Combine(root,name);File.WriteAllText(path,text,new UTF8Encoding(false));return path;}
void Until(Func<bool> condition,int timeout=7000){var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<timeout){if(condition())return;Thread.Sleep(15);}throw new Exception("Timed out.");}
try
{
if(args.Contains("--from-executable"))Test("Published executable runs tests against its own loaded application assembly",()=>True(System.Reflection.Assembly.GetEntryAssembly()==typeof(Document).Assembly));
if(args.Contains("--commands-only")){CommandTests.Run(Test,root);}
else
{
WindowsInputTests.Run(Test,root);
TextBarTests.Run(Test,root);
UiIconTests.Run(Test,root);
TagStoreTests.Run(Test,root);
ErrorLogTests.Run(Test,root);
CommandTests.Run(Test,root);
Test("empty document coordinates",()=>{var d=new Document();Eq(d.Position,(0,0));d.Move(-1,0,false);Eq(d.Cursor,0);});
Test("selection replacement and undo redo",()=>{var d=new Document();d.Insert("hello world");d.Anchor=6;d.Cursor=11;d.Insert("Rudy");Eq(d.Text,"hello Rudy");d.Undo();Eq(d.Text,"hello world");d.Redo();Eq(d.Text,"hello Rudy");});
Test("UTF-8 CRLF BOM round trip and dirty state",()=>{var p=Path.Combine(root,"bom.md");File.WriteAllText(p,"one\r\ntwo\r\n",new UTF8Encoding(true));var d=Document.Open(p);Eq(d.Text,"one\ntwo\n");Eq(d.NewLine,"\r\n");d.Cursor=d.Text.Length;d.Insert("three");True(d.Dirty);d.Save();True(!d.Dirty);var bytes=File.ReadAllBytes(p);True(bytes.Take(3).SequenceEqual(new byte[]{239,187,191}));Eq(File.ReadAllText(p),"one\r\ntwo\r\nthree");});
Test("surrogate and combining character deletion",()=>{var d=new Document();d.Insert("a😀e\u0301");d.Backspace();Eq(d.Text,"a😀");d.Backspace();Eq(d.Text,"a");d.Cursor=0;d.Delete();Eq(d.Text,"");});
Test("line navigation preserves valid positions",()=>{var d=new Document();d.Insert("one\nx\nthree");d.SetPosition(1,99);Eq(d.Position,(1,1));d.Move(0,1,true);Eq(d.Position,(2,1));True(d.Selection.Length>0);});
Test("Markdown wrappers place future text inside",()=>{var d=new Document();d.Wrap("**","**");d.Insert("bold");Eq(d.Text,"**bold**");d.Anchor=2;d.Cursor=6;d.Wrap("<u>","</u>");Eq(d.Text,"**<u>bold</u>**");});
Test("ordered and bulleted lists",()=>{var d=new Document();d.Insert("one\ntwo");d.Anchor=0;d.PrefixLines("",true);Eq(d.Text,"1. one\n2. two");});
Test("find wraps and selects",()=>{var d=new Document();d.Insert("hello there hello");True(d.Find("hello"));Eq(d.Selection,new Selection(0,5));True(d.Find("hello"));Eq(d.Selection,new Selection(12,5));True(!d.Find("absent"));});
Test("external edits cannot be silently overwritten",()=>{var p=FileAt("external.txt","original");var d=Document.Open(p);d.Insert("new");File.WriteAllText(p,"external");Throws(()=>d.Save());Eq(File.ReadAllText(p),"external");d.Save(overwriteExternal:true);Eq(File.ReadAllText(p),"neworiginal");});
Test("Save As protects existing destination",()=>{var p=FileAt("existing.md","keep");var d=new Document();d.Insert("replace");Throws(()=>d.Save(p));Eq(File.ReadAllText(p),"keep");});
Test("reject binary and invalid UTF8",()=>{var p=Path.Combine(root,"binary");File.WriteAllBytes(p,[0,1,2]);Throws(()=>Document.Open(p));File.WriteAllBytes(p,[255,254]);Throws(()=>Document.Open(p));});
Test("settings defaults and branch parser",()=>{Settings.EnsureDefaults();var s=Settings.Load(Settings.ConfigPath);Eq(s.DefaultTab,5);True(s.DevelopmentConsole&&!s.TextConsole);Eq(s.DefaultKeysMode,"Code");True(Path.IsPathRooted(Settings.ConfigDirectory));});
Test("invalid settings are rejected",()=>{Throws(()=>Settings.Load(FileAt("bad.cs","DefaultTab = 0;")));Throws(()=>Settings.Load(FileAt("bad.cs","Unknown = 5;")));Throws(()=>Settings.Load(FileAt("bad.cs","DefaultMode = unknown;")));});
Test("provided XML chords and scoped actions",()=>{var b=KeyBindings.Load(Settings.KeysPath);True(b.Any(x=>x.Command=="next-pane"&&x.Keys.SequenceEqual(new[]{"ctrl+k","p","right"})));True(b.Any(x=>x.Command=="fm-find"));True(b.Any(x=>x.Command=="shortcuts"));True(b.Any(x=>x.Command=="files"&&x.Keys[0]=="shift+space"));});
Test("XML unknown and duplicate commands rejected",()=>{Throws(()=>KeyBindings.Load(FileAt("bad.xml","<KeyboardShortcuts><Binding><Name>does-not-exist</Name><Keys>F1</Keys></Binding></KeyboardShortcuts>")));});
Test("language detection and keyword completion",()=>{Eq(Languages.Detect("hello.tsx"),"react");Eq(Languages.Detect("schema.prisma"),"prisma");Eq(Languages.Detect("test.CS"),"csharp");var d=Document.Open(FileAt("hello.py","pri"));d.Cursor=3;True(Languages.Complete(d).Contains("print"));});
Test("English dictionary and suggestions",()=>{var sp=new SpellChecker();True(sp.Available);True(!sp.Unknown("hello world").Any());True(sp.Unknown("helllo").Any());True(sp.Suggest("helllo").Contains("hello"));});
Test("key decoding modifiers and enhanced ShiftSpace",()=>{Eq(InputParser.EscapeSequence("[1;6C").Key,"ctrl+shift+right");Eq(InputParser.EscapeSequence("[32;2u").Key,"shift+space");Eq(InputParser.EscapeSequence("[24~").Key,"f12");Eq(KeyBindings.Normalize("Ctrl+k"),"ctrl+k");Eq(KeyBindings.Normalize("Right Arrow"),"right");});
Test("Windows arrow events use the editor's canonical names with all modifiers",()=>{
    foreach(var (key,name) in new[]{(ConsoleKey.LeftArrow,"left"),(ConsoleKey.RightArrow,"right"),(ConsoleKey.UpArrow,"up"),(ConsoleKey.DownArrow,"down")})
    for(int mods=0;mods<8;mods++)
    {
        bool shift=(mods&1)!=0,alt=(mods&2)!=0,ctrl=(mods&4)!=0;
        var e=InputParser.ConsoleKey(new ConsoleKeyInfo('\0',key,shift,alt,ctrl));
        Eq(e.Key,(ctrl?"ctrl+":"")+(alt?"alt+":"")+(shift?"shift+":"")+name);Eq(e.Text,"");
    }
});
Test("Windows quit and menu events match global shortcuts",()=>{
    Eq(InputParser.ConsoleKey(new ConsoleKeyInfo('\x11',ConsoleKey.Q,false,false,true)),new KeyEvent("ctrl+q"));
    foreach(char c in "femtvbh")
    {
        var key=Enum.Parse<ConsoleKey>(c.ToString().ToUpperInvariant());
        Eq(InputParser.ConsoleKey(new ConsoleKeyInfo(c,key,false,true,false)).Key,"alt+"+c);
    }
    Eq(InputParser.ConsoleKey(new ConsoleKeyInfo('A',ConsoleKey.A,true,false,false)),new KeyEvent("shift+a","A"));
});
Test("Ghostty Kitty flag 1 menu and quit sequences including lock modifiers",()=>{
    // Kitty modifier values are one plus a bitmask; lock keys must not hide shortcuts.
    foreach(int locks in new[]{0,64,128,192})
    {
        Eq(InputParser.EscapeSequence($"[113;{5+locks}u"),new KeyEvent("ctrl+q"));
        foreach(char c in "femtvbh")
            Eq(InputParser.EscapeSequence($"[{(int)c};{3+locks}u"),new KeyEvent("alt+"+c));
    }
    Eq(InputParser.EscapeSequence("[27u"),new KeyEvent("esc"));
    Eq(InputParser.EscapeSequence("[1;1D"),new KeyEvent("left"));
    Eq(InputParser.EscapeSequence("[1;1Q"),new KeyEvent("f2"));
});
Test("function keys accept omitted Kitty parameters as well as legacy encodings",()=>{
    for(int n=1;n<=4;n++)
    {
        char suffix=(char)('P'+n-1);
        foreach(string seq in new[]{"O"+suffix,"["+suffix,"[1"+suffix,"[1;1"+suffix,$"[{10+n}~"})
            Eq(InputParser.EscapeSequence(seq),new KeyEvent("f"+n));
        Eq(InputParser.EscapeSequence("[1;2"+suffix),new KeyEvent("shift+f"+n));
        Eq(InputParser.EscapeSequence("[1;5"+suffix),new KeyEvent("ctrl+f"+n));
    }
});
Test("Close Tab default is explicit in XML and available to existing installations",()=>{
    string bundled=Path.Combine(AppContext.BaseDirectory,"assets","KeyboardShortcuts.xml");
    var xml=System.Xml.Linq.XDocument.Load(bundled);
    True(xml.Root!.Elements("Binding").Any(b=>b.Element("Name")?.Value=="Close Tab"&&KeyBindings.Normalize(b.Element("Keys")!.Value)=="ctrl+shift+w"));
    var previous=FileAt("old-keys.xml","<KeyboardShortcuts><Binding><Name>Help</Name><Keys>F1</Keys></Binding></KeyboardShortcuts>");
    foreach(string path in new[]{bundled,previous})
        True(KeyBindings.Load(path).Any(b=>b.Command=="close-tab"&&b.Keys.SequenceEqual(new[]{"ctrl+shift+w"})));
    Eq(InputParser.ConsoleKey(new ConsoleKeyInfo('\x17',ConsoleKey.W,true,false,true)).Key,"ctrl+shift+w");
    Eq(InputParser.EscapeSequence("[119;6u").Key,"ctrl+shift+w");
});
Test("render control text cannot inject escape sequences",()=>{var s=new Screen(10,2,"#ffffff","#000000");s.Put(0,0,"a\x1b[31mb");True(s.Rows()[0].Contains("a?[31mb"));Eq(Screen.DisplayWidth("你😀"),4);});
Test("SGR mouse coordinates, drag, release, shift and wheel",()=>{
    Eq(InputParser.EscapeSequence("[<0;12;3M").Mouse,new MouseEvent(11,2,MouseAction.Press));
    Eq(InputParser.EscapeSequence("[<32;17;4M").Mouse,new MouseEvent(16,3,MouseAction.Move));
    Eq(InputParser.EscapeSequence("[<0;17;4m").Mouse,new MouseEvent(16,3,MouseAction.Release));
    Eq(InputParser.EscapeSequence("[<4;1;1M").Mouse,new MouseEvent(0,0,MouseAction.Press,Shift:true));
    Eq(InputParser.EscapeSequence("[<64;3;4M").Mouse,new MouseEvent(2,3,MouseAction.Wheel,Wheel:-1));
    Eq(InputParser.EscapeSequence("[<65;3;4M").Mouse,new MouseEvent(2,3,MouseAction.Wheel,Wheel:1));
    foreach(string seq in new[]{"[<0;0;1M","[<0;999999999999;1M","[<junkM","[<128;2;2M"})Eq(InputParser.EscapeSequence(seq).Key,"unknown");
});
Test("Windows input records preserve keys, mouse actions and viewport offsets",()=>{
    Eq(System.Runtime.InteropServices.Marshal.SizeOf<WindowsConsoleInput.Record>(),20);
    var input=new WindowsConsoleInput();
    Eq(input.Decode(new(){Type=1,KeyDown=1,VirtualKey=(ushort)ConsoleKey.W,Character='\x17',KeyControls=24})!.Value.Key,"ctrl+shift+w");
    foreach(var (key,controls,name) in new[]{(ConsoleKey.F1,0u,"f1"),(ConsoleKey.F2,0u,"f2"),(ConsoleKey.LeftArrow,0u,"left"),(ConsoleKey.Q,8u,"ctrl+q"),(ConsoleKey.F,2u,"alt+f"),(ConsoleKey.RightArrow,10u,"ctrl+alt+right")})
        Eq(input.Decode(new(){Type=1,KeyDown=1,VirtualKey=(ushort)key,KeyControls=controls})!.Value.Key,name);
    True(input.Decode(new(){Type=1,KeyDown=0,VirtualKey=(ushort)ConsoleKey.W})==null);
    Eq(input.Decode(new(){Type=2,X=15,Y=20,Buttons=1},5,10)!.Value.Mouse,new MouseEvent(10,10,MouseAction.Press));
    Eq(input.Decode(new(){Type=2,X=16,Y=21,Buttons=1,MouseFlags=1},5,10)!.Value.Mouse,new MouseEvent(11,11,MouseAction.Move));
    Eq(input.Decode(new(){Type=2,X=16,Y=21,Buttons=0},5,10)!.Value.Mouse,new MouseEvent(11,11,MouseAction.Release));
    Eq(input.Decode(new(){Type=2,X=5,Y=6,Buttons=120u<<16,MouseFlags=4})!.Value.Mouse,new MouseEvent(5,6,MouseAction.Wheel,Wheel:-1));
});
Test("mouse hit testing maps terminal cells to complete graphemes and tabs",()=>{
    string line="a\t你😀e\u0301z";
    Eq(Screen.ColumnAtCell(line,0,4),0);Eq(Screen.ColumnAtCell(line,2,4),1);
    Eq(Screen.ColumnAtCell(line,4,4),2);Eq(Screen.ColumnAtCell(line,5,4),2);
    Eq(Screen.ColumnAtCell(line,6,4),3);Eq(Screen.ColumnAtCell(line,7,4),3);
    Eq(Screen.ColumnAtCell(line,8,4),5);Eq(Screen.ColumnAtCell(line,9,4),7);
    Eq(Screen.ColumnAtCell(line,99,4),line.Length);
});
Test("default tab bindings use Ctrl Alt arrows",()=>{
    var xml=System.Xml.Linq.XDocument.Load(Path.Combine(AppContext.BaseDirectory,"assets","KeyboardShortcuts.xml"));
    foreach(var (name,key) in new[]{("Go To Next Tab","ctrl+alt+right"),("Go To Previous Tab","ctrl+alt+left")})
        Eq(KeyBindings.Normalize(xml.Root!.Elements("Binding").Single(b=>b.Element("Name")?.Value==name).Element("Keys")!.Value),key);
});
Test("pane splitting and removal",()=>{var l=new Layout();var first=l.Panes.First();var next=l.Split(first,true);Eq(l.Panes.Count(),2);var rectangles=l.Arrange(new(0,0,100,20));Eq(rectangles[first].W,50);True(l.Remove(next));Eq(l.Panes.Count(),1);});
Test("file copy, move, and overwrite guard",()=>{var from=FileAt("copy.txt","hello");string to=Path.Combine(root,"copy2.txt");FileBrowser.CopyPath(from,to,false);Eq(File.ReadAllText(to),"hello");Throws(()=>FileBrowser.CopyPath(from,to,false));string moved=Path.Combine(root,"copy3.txt");FileBrowser.CopyPath(to,moved,true);True(!File.Exists(to));True(File.Exists(moved));});
Test("directory self-copy blocked",()=>{string dir=Path.Combine(root,"dir");Directory.CreateDirectory(dir);Throws(()=>FileBrowser.CopyPath(dir,Path.Combine(dir,"child"),false));});
Test("build plans and nearest project discovery",()=>{string dir=Path.Combine(root,"project");Directory.CreateDirectory(Path.Combine(dir,"src"));File.WriteAllText(Path.Combine(dir,"sample.csproj"),"<Project/>");string source=Path.Combine(dir,"src","Program.cs");File.WriteAllText(source,"");var plan=BuildTools.Plan(source,false);Eq(plan.Executable,"dotnet");Eq(plan.Args[0],"build");Eq(plan.WorkingDirectory,dir);Eq(BuildTools.Plan(Path.Combine(root,"hello.py"),true).Args[0],"-u");});
Test("compiler diagnostic parsing",()=>{var d=BuildTools.ParseDiagnostic("main.c:3:9: error: bad",root)!;Eq(d.Line,3);Eq(d.Column,9);True(d.File.EndsWith("main.c"));var cs=BuildTools.ParseDiagnostic("Program.cs(12,5): error CS1002: ; expected",root)!;Eq(cs.Line,12);});
Test("real process execution and standard input",()=>{using var r=new ProcessRunner();r.Start(new(BuildTools.Python,["-u","-c","print(input().upper())"],root));r.Input("hello");Until(()=>!r.Active);Until(()=>r.Output.Any(x=>x=="HELLO"));});
Test("real Python line debugging, breakpoints, locals",()=>{string file=FileAt("debug.py","value = 1\nvalue += 2\nprint(value)\n");using var dbg=new DebuggerSession();dbg.Start(file,null,new(){{file,new(){3}}});DebugEvent? stopped=null;Until(()=>{while(dbg.Events.TryDequeue(out var e))if(e.Kind=="stopped"){stopped=e;return true;}return false;});Eq(stopped!.Line,1);dbg.Resume("next");Until(()=>{while(dbg.Events.TryDequeue(out var e))if(e.Kind=="stopped"){stopped=e;return true;}return false;});Eq(stopped!.Line,2);True(stopped.Text.Contains("value"));dbg.Resume("continue");Until(()=>{while(dbg.Events.TryDequeue(out var e))if(e.Kind=="stopped"){stopped=e;return true;}return false;});Eq(stopped!.Line,3);dbg.Resume("continue");Until(()=>!dbg.Active);});
Test("LSP framing, UTF-16 completion edits and live diagnostics",()=>{
    string fixture=Fixture("mock_lsp.py");
    File.WriteAllText(Path.Combine(Settings.ConfigDirectory,"languages.json"),System.Text.Json.JsonSerializer.Serialize(new Dictionary<string,object>{{"python",new{command=BuildTools.Which(BuildTools.Python)!,args=new[]{fixture}}}}));
    var document=Document.Open(FileAt("lsp.py","😀 pri"));document.Cursor=document.Text.Length;
    using var lsp=new LanguageServer("python",root);lsp.Sync(document);
    var result=lsp.Complete(document);Eq(result[0].Label,"print");Eq(result[0].Start,3);Eq(result[0].Length,3);
    Until(()=>lsp.Diagnostics.Any(d=>d.Message=="fixture diagnostic"));
});
Test("DAP request responses, asynchronous stop and stack frame",()=>{
    string fixture=Fixture("mock_dap.py");
    var events=new System.Collections.Concurrent.ConcurrentQueue<System.Text.Json.JsonElement>();
    using var rpc=new JsonRpcProcess(BuildTools.Python,[fixture],root,true);rpc.Message+=events.Enqueue;
    rpc.Request("initialize",new{}).GetAwaiter().GetResult();
    rpc.Request("launch",new{}).GetAwaiter().GetResult();
    rpc.Request("configurationDone",new{}).GetAwaiter().GetResult();
    Until(()=>events.Any(e=>e.GetProperty("event").GetString()=="stopped"));
    var frames=rpc.Request("stackTrace",new{threadId=1}).GetAwaiter().GetResult();Eq(frames.GetProperty("stackFrames")[0].GetProperty("line").GetInt32(),7);
});
if(Environment.GetEnvironmentVariable("RUDYEDIT_DAP_FIXTURE_DLL") is{} fixtureDll)
Test("real netcoredbg launch, source stepping and locals",()=>{
    string source=Environment.GetEnvironmentVariable("RUDYEDIT_DAP_FIXTURE_SOURCE")!;
    using var dbg=new DebuggerSession();dbg.Start(source,fixtureDll,new(){{source,new(){7}}});
    DebugEvent? stopped=null;
    bool StopEvent(){while(dbg.Events.TryDequeue(out var e)){if(e.Kind=="stopped"){stopped=e;return true;}if(e.Kind=="output")Console.WriteLine("  DAP: "+e.Text);}return false;}
    Until(StopEvent,15000);True(stopped!.Line>0);Eq(Path.GetFileName(stopped.File),"Program.cs");
    int first=stopped.Line;dbg.Resume("next");Until(StopEvent,15000);True(stopped!.Line>=first);
    dbg.Variables();Until(()=>dbg.Events.Any(e=>e.Kind=="variables"));
    dbg.Resume("continue");Until(StopEvent,15000);Eq(stopped!.Line,7);
    dbg.Resume("continue");Until(()=>!dbg.Active,15000);
});
Test("offline manuals and font included",()=>{True(File.Exists(Path.Combine(AppContext.BaseDirectory,"assets","fonts","FiraCodeNerdFontMono-Regular.ttf")));foreach(var l in Languages.All)True(File.Exists(Path.Combine(AppContext.BaseDirectory,"manuals",l.Id+".md"))||l.Id=="json");});
}
}
finally
{
    try{Directory.Delete(root,true);}catch(Exception ex){Test("Test workspace cleanup",()=>throw new IOException("Could not remove temporary test workspace",ex));}
    if(reportPath!=null){Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);File.WriteAllText(reportPath,System.Text.Json.JsonSerializer.Serialize(new{schema=1,passed,errors=failed,command_coverage=CommandTests.Covered.Order().ToArray(),results},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));}
}
Console.WriteLine($"\n{passed} passed; {failed} failed.");return failed>0?1:0;
