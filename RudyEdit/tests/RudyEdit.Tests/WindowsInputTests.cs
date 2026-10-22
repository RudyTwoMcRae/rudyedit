using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using RudyEdit;
using Record = RudyEdit.WindowsConsoleInput.Record;

static class WindowsInputTests
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Equal<T>(T actual, T expected) => Check(Equals(actual, expected), $"Expected {expected}, got {actual}");
    static Record Key(ConsoleKey key, uint controls = 0, char character = '\0', ushort repeat = 1) =>
        new() { Type = 1, KeyDown = 1, Repeat = repeat, VirtualKey = (ushort)key, Character = character, KeyControls = controls };
    static List<KeyEvent> Drain(WindowsConsoleInput input)
    {
        var result = new List<KeyEvent>(); while (input.TryRead() is {} key) result.Add(key); return result;
    }
    static void Sequence(WindowsConsoleInput input, string text, bool packet = false)
    {
        foreach (char c in text) input.Feed(Key(packet ? (ConsoleKey)231 : 0, character:c));
    }
    public static void Run(Action<string,Action> test, string root)
    {
        test("Windows stream regression: Ctrl+Q/S and F1 become single commands, not escape text", () =>
        {
            foreach (bool packet in new[]{false,true})
            foreach (var (seq,name) in new[]{("\x1b[113;5u","ctrl+q"),("\x1b[115;5u","ctrl+s"),("\x1bOP","f1"),("\x1b[1;2P","shift+f1"),("\x1b[27;5;113~","ctrl+q")})
            {
                var input = new WindowsConsoleInput(); Sequence(input,seq,packet);
                Equal(string.Join(',',Drain(input).Select(k=>k.Key)),name);
            }
        });
        test("Windows F1-F24 and Ctrl/Alt/Shift with both modifier sides and lock states", () =>
        {
            for(int n=1;n<=24;n++) for(int mods=0;mods<8;mods++)
            foreach(bool right in new[]{false,true}) foreach(uint locks in new[]{0u,0x1e0u})
            {
                uint flags=locks|((mods&1)!=0?16u:0)|((mods&2)!=0?(right?1u:2u):0)|((mods&4)!=0?(right?4u:8u):0);
                var input=new WindowsConsoleInput();input.Feed(Key((ConsoleKey)(111+n),flags));
                Equal(input.TryRead(),new KeyEvent(Mods(mods)+"f"+n));Equal(input.TryRead(),null);
            }
        });
        test("Windows Ctrl A-Z, arrows, digits, punctuation and modified text suppression", () =>
        {
            for(char c='a';c<='z';c++) foreach(uint ctrl in new[]{4u,8u}) foreach(uint shift in new[]{0u,16u}) foreach(char typed in new[]{c,(char)(c-'a'+1)})
            {
                var input=new WindowsConsoleInput(); input.Feed(Key((ConsoleKey)char.ToUpperInvariant(c),ctrl|shift,typed));
                Equal(input.TryRead(),new KeyEvent("ctrl+"+(shift!=0?"shift+":"")+c));
            }
            foreach(var (vk,name) in new[]{(ConsoleKey.LeftArrow,"left"),(ConsoleKey.RightArrow,"right"),(ConsoleKey.UpArrow,"up"),(ConsoleKey.DownArrow,"down"),(ConsoleKey.Home,"home"),(ConsoleKey.End,"end"),(ConsoleKey.PageUp,"pageup"),(ConsoleKey.PageDown,"pagedown"),(ConsoleKey.Delete,"delete"),(ConsoleKey.Insert,"insert"),(ConsoleKey.Oem3,"`"),(ConsoleKey.D1,"1"),(ConsoleKey.OemMinus,"-")})
            for(int mods=0;mods<8;mods++)
            {
                var input=new WindowsConsoleInput();input.Feed(Key(vk,Flags(mods)));
                Equal(input.TryRead(),new KeyEvent(Mods(mods)+name));
            }
            var altgr=new WindowsConsoleInput();altgr.Feed(Key(ConsoleKey.Q,9,'@'));Equal(altgr.TryRead(),new KeyEvent("@","@"));
        });
        test("Windows records: repeats, key-up/modifiers ignored, UTF-16, fragmented escapes and paste", () =>
        {
            long now=0; var input=new WindowsConsoleInput(()=>now);
            input.Feed(Key(ConsoleKey.A,character:'a',repeat:3)); Equal(string.Concat(Drain(input).Select(k=>k.Text)),"aaa");
            var up=Key(ConsoleKey.Q,8,'\x11');up.KeyDown=0;input.Feed(up);
            foreach(ushort vk in new ushort[]{16,17,18,20,91,92,144,145,160,161,162,163,164,165})input.Feed(Key((ConsoleKey)vk));
            Equal(input.TryRead(),null);
            Sequence(input,"\x1b[11"); Equal(input.TryRead(),null);now+=20;Sequence(input,"3;5u");Equal(input.TryRead(),new KeyEvent("ctrl+q"));
            Sequence(input,"\x1b");Equal(input.TryRead(),null);now+=61;Equal(input.TryRead(),new KeyEvent("esc"));
            Sequence(input,"😀",true);Equal(input.TryRead(),new KeyEvent("😀","😀"));
            Sequence(input,"\x1b[200~line1\n\x11\x1b[113;5u");Equal(input.TryRead(),null);
            Sequence(input,"\x1b[201~");Equal(input.TryRead(),new KeyEvent("paste-text","line1\n\x11\x1b[113;5u",true));
            Sequence(input,"\x1b[");input.Feed(Key(ConsoleKey.Q,8,'\x11'));
            Equal(input.TryRead(),new KeyEvent("unknown"));Equal(input.TryRead(),new KeyEvent("ctrl+q"));
            Sequence(input,"\x1b");input.Feed(Key(ConsoleKey.F2));Equal(input.TryRead(),new KeyEvent("esc"));Equal(input.TryRead(),new KeyEvent("f2"));
        });
        test("Terminal setup keeps Windows in native mode, handles Ctrl+C/mouse, and restores output", () =>
        {
            string windows=Terminal.StartupSequence(true),unix=Terminal.StartupSequence(false);
            Check(!windows.Contains("[>1u")&&!windows.Contains("[?2004h"),"Windows must not request encoded keyboard/paste");
            Check(windows.Contains("[>0u"),"Reset inherited keyboard mode for native records");
            Check(unix.Contains("[>1u")&&unix.Contains("[?1006h"),"Linux protocol remains enabled");
            for(uint mode=0;mode<=0x3ff;mode++)
            {
                uint configured=Terminal.WindowsInputMode(mode);
                Check((configured&0x247)==0,"Processed, line, echo, quick edit and VT input must be disabled");
                Check((configured&0x98)==0x98,"Mouse, window and extended flags must be enabled");
            }
            foreach(string suffix in new[]{"[<u","[?2004l","[?25h","[?1049l"})Check(Terminal.ShutdownSequence.Contains(suffix),"Cleanup "+suffix);
        });
        test("CSI decoder rejects malformed numbers, release events and unsupported functional keys", () =>
        {
            foreach(string seq in new[]{"[999999999999;5u","[1114112u","[55296u","[113;0u","[113;999999999999u","[1;999999999999C","[113;5:3u","[57441u","[97;1;13u"})
                Equal(InputParser.EscapeSequence(seq),new KeyEvent("unknown"));
            Equal(InputParser.EscapeSequence("[97:65;2u"),new KeyEvent("shift+a","A"));
            Equal(InputParser.EscapeSequence("[97;2;65u"),new KeyEvent("shift+a","A"));
            Equal(InputParser.EscapeSequence("[113;5:2u"),new KeyEvent("ctrl+q"));
            for(int n=1;n<=24;n++)Equal(InputParser.EscapeSequence($"[{57363+n};5u"),new KeyEvent("ctrl+f"+n));
            var random=new Random(42);
            for(int i=0;i<10000;i++)
            {
                string seq="["+string.Concat(Enumerable.Range(0,random.Next(1,60)).Select(_=>"0123456789;:<>?"[random.Next(14)]))+"u";
                InputParser.EscapeSequence(seq); // Must never throw on terminal input.
            }
        });
        test("Failed build/run launch cannot poison Active, Stop or Quit", () =>
        {
            using var runner=new ProcessRunner();
            bool failed=false;
            try{runner.Start(new ToolCommand(Path.Combine(root,"missing-tool-does-not-exist"),[],root));}
            catch(System.ComponentModel.Win32Exception){failed=true;}
            Check(failed,"Missing tool must fail");Check(!runner.Active,"Failed launch remained active");runner.Stop();runner.Dispose();
            Check(!runner.Active,"Disposed runner must be inactive");
            Equal(BuildTools.ParseDiagnostic("file.cs(999999999999999,1): error",root),null);
            Equal(BuildTools.ParseDiagnostic("file.cs:1:999999999999999: error",root),null);
        });
        foreach(string protocol in new[]{"native","csi","legacy"}) foreach(bool vim in new[]{false,true})
        {
            string tag=protocol+(vim?"-vim":"-code");
            test("Windows editor commands, save and quit through "+tag,()=>EditorFlow(root,protocol,vim));
            test("Windows quit saves unsaved work through "+tag,()=>SaveQuitFlow(root,protocol,vim));
            foreach(string state in new[]{"menu","help","files","console","chord","save-as","open","quit-save-as"})
                test("Windows safe quit/cancel in "+state+" through "+tag,()=>QuitFlow(root,protocol,vim,state));
        }
        foreach(string protocol in new[]{"native","csi"}) foreach(bool vim in new[]{false,true})
        foreach(bool vertical in new[]{false,true}) foreach(string shortcut in new[]{"ctrl+w"})
            test($"Remove panel via {shortcut}, {protocol}, VIM={vim}, vertical={vertical}",()=>PanelFlow(root,protocol,vim,vertical,shortcut));
        foreach(string protocol in new[]{"native","legacy","csi"}) foreach(bool vim in new[]{false,true})
            test($"Open config folder shortcut: {protocol}, VIM={vim}",()=>ConfigFolderFlow(root,protocol,vim));
        test("Editor command failures are recorded in the persistent error log",()=>
        {
            var t=new ScriptTerminal("native");for(int i=0;i<8;i++)t.Send("ctrl+alt+v");
            t.Expect("Maximum of eight panes.");t.Do(()=>Check(ErrorLog.Read().Contains("Maximum of eight panes."),"Editor error was not logged"));t.Send("ctrl+q");
            using(var app=new EditorApp(root,null,false,t))app.Run();t.Complete();
        });
        test("Config fallback preserves the exception in the error log",()=>
        {
            string original=File.ReadAllText(Settings.ConfigPath);
            try
            {
                File.WriteAllText(Settings.ConfigPath,"DefaultTab = 0;");var t=new ScriptTerminal("native");t.Expect("Configuration error");t.Send("esc");
                t.Do(()=>Check(ErrorLog.Read().Contains("Load Config.cs")&&ErrorLog.Read().Contains("DefaultTab must be"),"Configuration failure was not logged"));t.Send("ctrl+q");
                using(var app=new EditorApp(root,null,false,t))app.Run();t.Complete();
            }
            finally{File.WriteAllText(Settings.ConfigPath,original);}
        });
        test("Tab and panel defaults upgrade both legacy shortcuts",()=>
        {
            foreach(string xml in new[]{"<KeyboardShortcuts/>","<KeyboardShortcuts><Binding><Name>Close Tab</Name><Keys>Ctrl+w</Keys><Context>Global</Context></Binding><Binding><Name>Close Panel</Name><Keys>Shift+Ctrl+w</Keys><Context>Global</Context></Binding></KeyboardShortcuts>"})
            {
                string path=Path.Combine(root,"panel-keys.xml");File.WriteAllText(path,xml);
                var bindings=KeyBindings.Load(path);
                foreach(string key in new[]{"ctrl+w","ctrl+shift+w"})
                    Equal(bindings.Single(b=>b.Context=="Global"&&b.Keys.SequenceEqual(new[]{key})).Command,key=="ctrl+w"?"close-pane":"close-tab");
                Equal(File.ReadAllText(path),xml);
            }
            var bundled=KeyBindings.Load(Path.Combine(AppContext.BaseDirectory,"assets","KeyboardShortcuts.xml"));
            foreach(string key in new[]{"ctrl+w","ctrl+shift+w"})Equal(bundled.Single(b=>b.Context=="Global"&&b.Keys.SequenceEqual(new[]{key})).Command,key=="ctrl+w"?"close-pane":"close-tab");
        });
        foreach(string protocol in new[]{"native","csi"}) foreach(bool vim in new[]{false,true})
        foreach(bool vertical in new[]{false,true}) foreach(bool save in new[]{false,true})
            test($"Close active tab, preserve panels and other tabs: {protocol}, VIM={vim}, vertical={vertical}, save={save}",()=>TabFlow(root,protocol,vim,vertical,save));
        test("Existing XML is migrated accurately and idempotently without changing custom bindings",()=>
        {
            string path=Path.Combine(root,"legacy-upgrade.xml");
            File.WriteAllText(path,"<KeyboardShortcuts><!-- keep comment --><Binding><Name>Close Panel</Name><Keys>Shift+Ctrl+w</Keys><Context>Global</Context></Binding><Binding><Name>Close Tab</Name><Keys>Ctrl+w</Keys><Context>Global</Context></Binding><Binding><Name>Save</Name><Keys>Ctrl+k, s</Keys><Context>Global</Context></Binding></KeyboardShortcuts>");
            KeyBindings.UpgradeLegacyCloseBindings(path);
            var xml=System.Xml.Linq.XDocument.Load(path);var items=xml.Root!.Elements("Binding").ToArray();
            Equal(items[0].Element("Name")!.Value,"Close Tab");Equal(items[1].Element("Name")!.Value,"Close Panel");
            Equal(items[2].Element("Name")!.Value,"Save");Equal(items[2].Element("Keys")!.Value,"Ctrl+k, s");
            Check(File.ReadAllText(path).Contains("keep comment"),"Comment lost");
            string once=File.ReadAllText(path);KeyBindings.UpgradeLegacyCloseBindings(path);Equal(File.ReadAllText(path),once);
        });
        test("Windows INPUT_RECORD raw binary layout matches Win32 offsets", () =>
        {
            Equal(Marshal.SizeOf<Record>(),20);
            byte[] bytes=new byte[20];BitConverter.GetBytes((ushort)1).CopyTo(bytes,0);BitConverter.GetBytes(1).CopyTo(bytes,4);
            BitConverter.GetBytes((ushort)1).CopyTo(bytes,8);BitConverter.GetBytes((ushort)81).CopyTo(bytes,10);
            BitConverter.GetBytes((ushort)16).CopyTo(bytes,12);BitConverter.GetBytes((ushort)17).CopyTo(bytes,14);BitConverter.GetBytes(8u).CopyTo(bytes,16);
            var pin=GCHandle.Alloc(bytes,GCHandleType.Pinned);
            try {var input=new WindowsConsoleInput();input.Feed(Marshal.PtrToStructure<Record>(pin.AddrOfPinnedObject()));Equal(input.TryRead(),new KeyEvent("ctrl+q"));}
            finally{pin.Free();}
        });
    }
    static string Mods(int mods)=>( (mods&4)!=0?"ctrl+":"")+((mods&2)!=0?"alt+":"")+((mods&1)!=0?"shift+":"");
    static uint Flags(int mods)=>((mods&4)!=0?8u:0)|((mods&2)!=0?2u:0)|((mods&1)!=0?16u:0);
    static void EditorFlow(string root,string protocol,bool vim)
    {
        string folder=Path.Combine(root,"flow-"+protocol+vim);Directory.CreateDirectory(folder);
        var t=new ScriptTerminal(protocol);
        t.Expect("Text | Code");t.Send("ctrl+shift+t");t.ExpectAbsent("Add tags:");t.Send("ctrl+shift+t");t.Expect("Add tags:");t.Send("f1");t.Expect("Context help");t.Send("esc");
        t.Send("shift+f1");t.Expect("Keybindings");t.Send("esc");t.Send("ctrl+f1");t.Expect("Offline guide");t.Send("esc");
        t.Send("f2");t.Expect("VIM NORMAL");t.Send("f2");t.Expect("Text | Code");
        if(vim){t.Send("f2");t.Send("i");}
        t.Type("alpha beta");if(vim)t.Send("esc");
        t.Send("left");t.Expect("| 1:10 |");t.Send("right");t.Expect("| 1:11 |");
        t.Send("ctrl+a");t.Send("ctrl+c");t.Send("ctrl+x");t.Send("ctrl+v");
        t.Send("ctrl+z");t.Send("ctrl+y");
        t.Send("ctrl+s");t.Expect("Save As");t.Send("ctrl+a");t.Type(Path.Combine(folder,"saved.txt"));t.Send("enter");t.Expect("Saved ");
        t.Do(()=>Equal(File.ReadAllText(Path.Combine(folder,"saved.txt")),"alpha beta"));
        t.Send("ctrl+f");t.Expect("Find text");t.Type("alpha");t.Send("enter");t.Send("f4");t.Expect("Match selected");t.Send("esc");
        t.Send("f3");t.Expect("Development |");
        foreach(string letter in new[]{"f","e","m","t","v","b","h"}) {t.Send("alt+"+letter);t.Expect(" "+(letter switch{"f"=>"File","e"=>"Edit","m"=>"Mode","t"=>"Text","v"=>"View","b"=>"Build",_=>"Help"})+" ");t.Send("esc");}
        t.Send("f7");t.Expect("Spelling");t.Send("esc");t.Send("f8");t.Send("esc");
        t.Send("f9");t.Expect("Breakpoint toggled");t.Send("f10");t.Expect("RudyEdit menu");t.Send("esc");
        t.Send("ctrl+n");
        foreach(string fn in new[]{"f5","f6","ctrl+f5"}){t.Send(fn);t.Expect("Save As");t.Send("esc");}
        t.Send("shift+f5");t.Expect("Stopped.");
        t.Send("f11");t.Send("shift+f11");t.Send("f12");
        t.Send("ctrl+alt+left");t.Expect("saved.txt");t.Send("ctrl+alt+right");
        t.Send("ctrl+shift+w");t.Send("shift+f2");t.Expect("KeyboardShortcuts.xml");
        t.Send("ctrl+f2");t.Expect("Config.cs");t.Send("ctrl+q");
        using(var app=new EditorApp(folder,null,false,t))app.Run();
        Check(t.Disposed,"Terminal not disposed");t.Complete();
    }
    static void ConfigFolderFlow(string root,string protocol,bool vim)
    {
        var t=new ScriptTerminal(protocol);string? opened=null;
        if(vim)t.Send("f2");
        t.Send("ctrl+alt+c");t.Expect("Configuration folder:");t.Expect("Config.cs");t.Expect("Tags.xml");t.Expect("RudyEdit-errors.log");
        t.Do(()=>Equal(opened,Path.GetFullPath(Settings.ConfigDirectory)));
        t.Send("f3");t.Send("ctrl+alt+c");t.Expect("Configuration folder:");t.Send("ctrl+q");
        using(var app=new EditorApp(root,null,false,t,path=>opened=path))app.Run();t.Complete();
    }
    static void TabFlow(string root,string protocol,bool vim,bool vertical,bool save)
    {
        string folder=Path.Combine(root,"tab-"+protocol+vim+vertical+save);Directory.CreateDirectory(folder);
        string first=Path.Combine(folder,"first.txt"),second=Path.Combine(folder,"second.txt"),third=Path.Combine(folder,"third.txt");
        File.WriteAllText(first,"first document");File.WriteAllText(second,"second document");File.WriteAllText(third,"third document");
        var t=new ScriptTerminal(protocol);if(vim)t.Send("f2");
        t.Send(vertical?"ctrl+alt+v":"ctrl+alt+h");t.ExpectPanels(2);
        foreach(string file in new[]{second,third}){t.Send("ctrl+o");t.Expect("Open file");t.Send("ctrl+a");t.Type(file);t.Send("enter");}
        t.Send("ctrl+alt+left");t.ExpectSelectedTab("second.txt");
        if(vim)t.Send("i");t.Type("edited ");if(vim)t.Send("esc");
        t.Send("ctrl+shift+w");t.Expect("Unsaved: second.txt");t.Send("down");t.Send("down");t.Send("enter");
        t.ExpectPanels(2);t.ExpectSelectedTab("second.txt");t.Expect("edited second document");
        t.Send("ctrl+shift+w");t.Expect("Unsaved: second.txt");if(!save)t.Send("down");t.Send("enter");
        t.ExpectPanels(2);t.ExpectNoTab("second.txt");t.ExpectSelectedTab("third.txt");
        t.Do(()=>Equal(File.ReadAllText(second),save?"edited second document":"second document"));
        t.Send("ctrl+alt+left");t.ExpectSelectedTab("first.txt");t.Send("ctrl+alt+right");t.ExpectSelectedTab("third.txt");
        t.Send("ctrl+shift+w");t.ExpectPanels(2);t.ExpectNoTab("third.txt");t.ExpectSelectedTab("first.txt");
        t.Send("ctrl+q");using(var app=new EditorApp(folder,first,false,t))app.Run();t.Complete();
    }
    static void PanelFlow(string root,string protocol,bool vim,bool vertical,string shortcut)
    {
        string folder=Path.Combine(root,"panel-"+protocol+vim+vertical+shortcut.Replace('+','-'));Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,"saved-panel-tab.txt");var t=new ScriptTerminal(protocol);
        if(vim)t.Send("f2");
        void Text(string value){if(vim)t.Send("i");t.Type(value);if(vim)t.Send("esc");}
        Text("left buffer");t.Send(vertical?"ctrl+alt+v":"ctrl+alt+h");t.ExpectPanels(2);
        t.Send("ctrl+n");Text("save this tab");t.Send("ctrl+n");Text("discard this tab");
        t.Send(shortcut);t.Expect("Unsaved:");t.Send("down");t.Send("down");t.Send("enter");t.ExpectPanels(2);t.Expect("discard this tab");
        t.Send(shortcut);t.Expect("Unsaved:");t.Send("enter");t.Expect("Save As");t.Send("ctrl+a");t.Type(path);t.Send("enter");
        t.Expect("Unsaved:");t.Send("down");t.Send("enter");t.ExpectPanels(1);t.Expect("left buffer");t.ExpectAbsent("discard this tab");
        t.Do(()=>Equal(File.ReadAllText(path),"save this tab"));
        t.Send("ctrl+q");t.Expect("Unsaved:");t.Send("down");t.Send("enter");
        using(var app=new EditorApp(folder,null,false,t))app.Run();t.Complete();Check(t.Disposed,"Terminal not disposed");
    }
    static void SaveQuitFlow(string root,string protocol,bool vim)
    {
        string folder=Path.Combine(root,"savequit-"+protocol+vim);Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,"saved-on-quit.txt");
        var t=new ScriptTerminal(protocol);if(vim){t.Send("f2");t.Send("i");}
        t.Type("saved on quit");if(vim)t.Send("esc");
        t.Send("ctrl+q");t.Expect("Unsaved:");t.Send("enter");t.Expect("Save As");
        t.Send("ctrl+a");t.Type(path);t.Send("enter");
        using(var app=new EditorApp(folder,null,false,t))app.Run();
        Check(t.Disposed,"Terminal not disposed");t.Complete();Equal(File.ReadAllText(path),"saved on quit");
    }
    static void QuitFlow(string root,string protocol,bool vim,string state)
    {
        string folder=Path.Combine(root,"quit-"+protocol+vim+state);Directory.CreateDirectory(folder);
        var t=new ScriptTerminal(protocol);if(vim){t.Send("f2");t.Send("i");}
        t.Type("keep me");if(vim)t.Send("esc");
        switch(state)
        {
            case "menu":t.Send("alt+f");break;case "help":t.Send("f1");break;
            case "files":t.Send("f8");break;case "console":t.Send("ctrl+`");break;
            case "chord":t.Send("ctrl+k");break;
            case "open":t.Send("ctrl+o");t.Expect("Open file");break;
            case "save-as":t.Send("ctrl+s");t.Expect("Save As");break;
            case "quit-save-as":t.Send("ctrl+q");t.Expect("Unsaved:");t.Send("enter");t.Expect("Save As");t.Send("esc");break;
        }
        t.Send("ctrl+q");t.Expect("Unsaved:");
        t.Send("ctrl+q");t.Send("alt+f");t.Expect("Unsaved:");
        t.Send("down");t.Send("down");t.Send("enter");t.ExpectAbsent("Unsaved:");
        t.Send("ctrl+q");t.Expect("Unsaved:");t.Send("down");t.Send("enter");
        using(var app=new EditorApp(folder,null,false,t))app.Run();
        Check(t.Disposed,"Terminal not disposed");t.Complete();
        Check(!File.Exists(Path.Combine(folder,"Untitled")),"Discard wrote a file");
    }
    sealed class ScriptTerminal(string protocol):ITerminal
    {
        readonly WindowsConsoleInput input=new();readonly Queue<Action> script=new();
        string screen="";Exception? failure;
        public int Width=>120;public int Height=>36;public bool Disposed;
        public void Dispose()=>Disposed=true;public void Invalidate(){}
        public void Draw(Screen s,int x=-1,int y=-1)=>screen=Regex.Replace(string.Join('\n',s.Rows()),"\x1b\\[[0-9;]*m","");
        public void Do(Action a)=>script.Enqueue(a);
        public void Expect(string text)=>Do(()=>Check(screen.Contains(text),"Missing: "+text+"\n"+screen));
        public void ExpectNoTab(string name)=>Do(()=>Check(!screen.Split('\n').Where(line=>line.Contains('▸')).Any(line=>line.Contains(name)),"Tab still present: "+name));
        public void ExpectSelectedTab(string name)=>Do(()=>Check(screen.Contains("▸ "+name),"Selected tab missing: "+name+"\n"+screen));
        public void ExpectPanels(int count)=>Do(()=>Equal(screen.Count(c=>c=='▸'),count));
        public void ExpectAbsent(string text)=>Do(()=>Check(!screen.Contains(text),"Unexpected: "+text));
        public void Type(string text)=>Do(()=>Sequence(input,text));
        public void Complete(){if(failure!=null)throw failure;Equal(script.Count,0);}
        public KeyEvent? ReadKey()
        {
            if(failure!=null)throw failure;
            try
            {
                while(true)
                {
                    if(input.TryRead() is {} key)return key;
                    if(script.Count==0)throw new Exception("Editor failed to exit after Ctrl+Q");
                    script.Dequeue()();
                }
            }
            catch(Exception ex){failure=ex;throw;}
        }
        public void Send(string name)=>Do(()=>SendNow(name));
        void SendNow(string name)
        {
            var parts=name.Split('+');string key=parts[^1];int mods=(parts.Contains("shift")?1:0)|(parts.Contains("alt")?2:0)|(parts.Contains("ctrl")?4:0);
            int code=key.Length==1?key[0]:key switch{"esc"=>27,"enter"=>13,"tab"=>9,"space"=>32,_=>0};
            int f=key.StartsWith('f')&&int.TryParse(key[1..],out int number)?number:0;
            ConsoleKey vk=f>0?(ConsoleKey)(111+f):key switch{"esc"=>ConsoleKey.Escape,"enter"=>ConsoleKey.Enter,"tab"=>ConsoleKey.Tab,"space"=>ConsoleKey.Spacebar,"left"=>ConsoleKey.LeftArrow,"right"=>ConsoleKey.RightArrow,"up"=>ConsoleKey.UpArrow,"down"=>ConsoleKey.DownArrow,"`"=>ConsoleKey.Oem3,_=>key.Length==1?(ConsoleKey)char.ToUpperInvariant(key[0]):throw new Exception("Unknown test key "+key)};
            if(protocol=="native")
            {
                // Use native VK for Esc so no artificial timer is needed in scripted tests.
                input.Feed(Key(vk,Flags(mods),code>=32?(char)code:'\0'));return;
            }
            string seq;
            if(f>0)
            {
                int[] tilde=[11,12,13,14,15,17,18,19,20,21,23,24];
                seq=protocol=="csi"?$"\x1b[{57363+f};{mods+1}u":f<=4&&mods==0?"\x1bO"+(char)('P'+f-1):$"\x1b[{tilde[f-1]};{mods+1}~";
            }
            else if(key is "left" or "right" or "up" or "down")seq=$"\x1b[1;{mods+1}"+(key switch{"left"=>"D","right"=>"C","up"=>"A",_=>"B"});
            else seq=$"\x1b[{code};{mods+1}u";
            Sequence(input,seq);
        }
    }
}
