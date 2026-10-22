using RudyEdit;
using System.Text.RegularExpressions;

static class UiIconTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    sealed class Terminal:ITerminal
    {
        readonly Queue<Func<KeyEvent?>> steps=[];
        public int Width{get;set;}=120;public int Height{get;set;}=36;
        public string Screen="",Raw="";
        public void Dispose(){} public void Invalidate(){}
        public void Draw(Screen s,int x=-1,int y=-1){Raw=string.Join('\n',s.Rows());Screen=Regex.Replace(Raw,"\x1b\\[[0-9;]*m","");}
        public KeyEvent? ReadKey()=>steps.Count>0?steps.Dequeue()():throw new Exception("Script exhausted");
        public void Key(string key,string text="")=>steps.Enqueue(()=>new KeyEvent(key,text));
        public void Do(Action action)=>steps.Enqueue(()=>{action();return null;});
        public void Expect(string text)=>Do(()=>Check(Screen.Contains(text),"Missing "+text+"\n"+Screen));
        public void Click(string text)=>steps.Enqueue(()=>
        {
            var lines=Screen.Split('\n');int y=Array.FindIndex(lines,l=>l.Contains(text));Check(y>=0,"Missing click target "+text);
            return new KeyEvent("mouse",Mouse:new MouseEvent(lines[y].IndexOf(text),y,MouseAction.Press));
        });
    }
    public static void Run(Action<string,Action> test,string root)
    {
        test("Every Nerd UI icon occupies one private-use cell",()=>
        {
            var icons=UiIcons.All.ToList();Check(icons.Count==78,"Unexpected catalog count");
            foreach(var i in icons)Check(i.Glyph.Length==1&&i.Glyph[0]>=0xe000&&i.Glyph[0]<=0xf8ff&&Screen.DisplayWidth(i.Glyph)==1,i.Name+" has unsafe width");
        });
        test("File icons classify languages, data, media and unknown extensions without reading files",()=>
        {
            foreach(var (name,icon) in new[]{("demo.CS",UiIcons.CSharp),("program.cpp",UiIcons.Cpp),("main.py",UiIcons.Python),("main.rs",UiIcons.Rust),("index.js",UiIcons.JavaScript),("index.ts",UiIcons.TypeScript),("app.tsx",UiIcons.React),("app.vue",UiIcons.Vue),("readme.MD",UiIcons.Markdown),("note.txt",UiIcons.Text),("data.json",UiIcons.Json),("data.xml",UiIcons.Xml),("query.sql",UiIcons.Database),("site.css",UiIcons.Css),("page.html",UiIcons.Html),("script.sh",UiIcons.Terminal),("script.ps1",UiIcons.PowerShell),("photo.png",UiIcons.Image),("book.pdf",UiIcons.Pdf),("doc.docx",UiIcons.Word),("data.xlsx",UiIcons.Spreadsheet),("deck.pptx",UiIcons.Presentation),("song.mp3",UiIcons.Audio),("movie.mp4",UiIcons.Video),("font.ttf",UiIcons.Font),("backup.tar.gz",UiIcons.Archive),("app.exe",UiIcons.Binary),("new.unknown",UiIcons.File)})
                Check(UiIcons.Entry(Path.Combine(root,"does-not-exist",name))==icon,"Wrong icon: "+name);
        });
        test("Special filename and folder icons take precedence over extensions",()=>
        {
            Check(UiIcons.Entry("Dockerfile.prod")==UiIcons.Docker&&UiIcons.Entry("CARGO.LOCK")==UiIcons.Rust&&UiIcons.Entry(".gitignore")==UiIcons.Git,"Special filename priority");
            foreach(string name in new[]{"Config.cs","Tags.xml","KeyboardShortcuts.xml",".env.local"})Check(UiIcons.Entry(name)==UiIcons.Config,name);
            foreach(var (name,icon) in new[]{(".git",UiIcons.GitFolder),(".github",UiIcons.GitHubFolder),("src",UiIcons.SourceFolder),("node_modules",UiIcons.NpmFolder),(".config",UiIcons.ConfigFolder),("photo.png",UiIcons.Folder)})Check(UiIcons.Entry(name,true)==icon,name);
            Check(UiIcons.Entry("folder",true,opened:true)==UiIcons.FolderOpen&&UiIcons.Entry(".git",true,parent:true)==UiIcons.Parent,"Open/parent folder");
        });
        test("Graphic icon settings default on and validate explicit boolean fallback",()=>
        {
            string p=Path.Combine(root,"icon-settings.cs");File.WriteAllText(p,"UseNerdIcons = false;");Check(!Settings.Load(p).UseNerdIcons,"Fallback ignored");
            File.WriteAllText(p,"");Check(Settings.Load(p).UseNerdIcons,"Upgrade default off");
            File.WriteAllText(p,"UseNerdIcons = invalid;");try{Settings.Load(p);}catch(FormatException){return;}throw new Exception("Invalid value accepted");
        });
        foreach(bool vim in new[]{false,true})
        test("Graphic menu clicks, file icons, plain fallback and font help "+(vim?"VIM":"Code"),()=>
        {
            string folder=Path.Combine(root,"icon-ui-"+vim);Directory.CreateDirectory(folder);Directory.CreateDirectory(Path.Combine(folder,"src"));
            foreach(string name in new[]{"main.cs","note.md","photo.png","script.py","data.json"})File.WriteAllText(Path.Combine(folder,name),"icon-opened-"+name);
            var t=new Terminal();if(vim)t.Key("f2");t.Key("f8");
            foreach(var icon in new[]{UiIcons.SourceFolder,UiIcons.CSharp,UiIcons.Markdown,UiIcons.Image,UiIcons.Python,UiIcons.Json})t.Expect(icon.Glyph);
            t.Click(UiIcons.Python.Glyph);t.Key("enter");t.Expect("icon-opened-script.py");
            t.Click(UiIcons.Menu("File").Glyph);t.Expect(UiIcons.Save.Glyph+" Save");t.Key("esc");
            t.Key("alt+v");t.Key("text","Toggle Graphic Icons");t.Key("enter");t.Key("f8");t.Expect("[PY] script.py");
            t.Do(()=>Check(!t.Screen.Any(c=>c>=0xe000&&c<=0xf8ff),"Private glyph remains in fallback"));
            t.Key("alt+h");t.Key("text","Nerd Font & Icons");t.Key("enter");t.Expect("FiraCode Nerd Font Mono");t.Key("esc");
            t.Key("ctrl+q");using var app=new EditorApp(folder,null,false,t);app.Run();
        });
        foreach(int width in new[]{20,40,80,120})
        test("Formatting icon keyboard access fits terminal width "+width,()=>
        {
            var t=new Terminal{Width=width};t.Key("ctrl+alt+m");
            foreach(string id in new[]{"font","font-size","bold","italic","underline","foreground","background","bullets","ordered","spell","toggle-spell","preview"})
            {t.Expect(UiIcons.Command(id).Glyph);t.Key("right");}
            t.Key("esc");t.Key("ctrl+q");using var app=new EditorApp(root,null,false,t);app.Run();
        });
    }
}
