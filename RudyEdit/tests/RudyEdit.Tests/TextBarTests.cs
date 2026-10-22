using RudyEdit;
using System.Text.RegularExpressions;

static class TextBarTests
{
    sealed class Terminal:ITerminal
    {
        readonly Queue<Func<KeyEvent?>> steps=[];
        public int Width{get;set;}=120;public int Height{get;set;}=36;
        public string Screen="";
        public void Dispose(){} public void Invalidate(){}
        public void Draw(Screen s,int x=-1,int y=-1)=>Screen=Regex.Replace(string.Join('\n',s.Rows()),"\x1b\\[[0-9;]*m","");
        public KeyEvent? ReadKey(){if(steps.Count==0)throw new Exception("Script exhausted");return steps.Dequeue()();}
        public void Key(string key,string text="")=>steps.Enqueue(()=>new KeyEvent(key,text));
        public void Do(Action action)=>steps.Enqueue(()=>{action();return null;});
        public void Expect(string text)=>Do(()=>{if(!Screen.Contains(text))throw new Exception("Missing "+text+"\n"+Screen);});
        public void Click(string text)=>steps.Enqueue(()=>
        {
            var lines=Screen.Split('\n');int y=Array.FindIndex(lines,l=>l.Contains(text));if(y<0)throw new Exception("Missing click target "+text);
            return new KeyEvent("mouse",Mouse:new MouseEvent(lines[y].IndexOf(text),y,MouseAction.Press));
        });
    }
    public static void Run(Action<string,Action> test,string root)
    {
        test("Tag metadata deduplicates, sanitizes and preserves cursor/selection with undo",()=>
        {
            var d=new Document();d.Insert("hello world");d.Cursor=8;d.Anchor=6;
            DocumentTags.Set(d,["Work","work"," project ","bad-->\n<tag>"]);
            if(DocumentTags.Read(d).Length!=3||d.Text.Substring(d.Selection.Start,d.Selection.Length)!="wo")throw new Exception("Tag metadata/selection mismatch");
            d.Undo();if(d.Text!="hello world")throw new Exception("Tag change is not undoable");
        });
        foreach(bool vim in new[]{false,true})
        test("Text bars add/save/search/clear tags and format via keyboard and mouse "+(vim?"VIM":"Code"),()=>
        {
            string folder=Path.Combine(root,"bars-"+vim);Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,"note.MD");File.WriteAllText(path,"hello world");
            File.WriteAllText(Path.Combine(folder,"other.txt"),"<!-- tags: Other -->\nother");
            var t=new Terminal();if(vim)t.Key("f2");
            t.Expect("Add tags:");t.Key("ctrl+shift+t");t.Do(()=>{if(t.Screen.Contains("Add tags:"))throw new Exception("Tag bar did not hide");});t.Key("ctrl+shift+t");t.Expect("Add tags:");t.Key("ctrl+alt+t");t.Key("ctrl+shift+t");t.Key("ctrl+alt+t");t.Key("f1");t.Expect("RudyEdit 2.0.6");t.Key("esc");t.Key("ctrl+alt+t");t.Key("text","Work, project, WORK");t.Key("enter");t.Expect("Tags: Work, project");
            t.Key("esc");t.Key("ctrl+s");t.Do(()=>{if(!DocumentTags.Read(Document.Open(path)).SequenceEqual(new[]{"Work","project"}))throw new Exception("Tags not saved");});
            t.Key("ctrl+alt+t");t.Key("text","unfinished");t.Key("esc");t.Key("ctrl+n");t.Key("ctrl+alt+t");t.Do(()=>{if(t.Screen.Contains("unfinished"))throw new Exception("Draft leaked across tabs");});t.Key("esc");t.Key("ctrl+alt+left");t.Expect("unfinished");
            t.Key("ctrl+alt+g");t.Key("text","WORK, project");t.Key("enter");t.Expect("1 files match all tags");t.Expect("note.MD");
            t.Key("ctrl+alt+g");t.Key("ctrl+a");t.Key("text","absent");t.Key("enter");t.Expect("0 files match all tags");
            t.Key("ctrl+alt+g");t.Key("ctrl+a");t.Key("enter");t.Expect("Tag filter cleared.");
            t.Key("esc");t.Key("ctrl+a");t.Click(UiIcons.Bold.Glyph);t.Expect("**hello world**");t.Key("ctrl+z");
            t.Click("Size ▾");t.Expect("Font size");t.Key("esc");
            t.Key("ctrl+alt+m");t.Key("right");t.Key("enter");t.Expect("Font size");t.Key("esc");
            t.Key("f3");t.Do(()=>{if(t.Screen.Contains("Add tags:")||t.Screen.Contains("Format ▾"))throw new Exception("Text bars visible in Development mode");});
            t.Key("f3");t.Do(()=>{t.Width=20;t.Height=8;});t.Expect("Add tags:");t.Do(()=>{t.Width=120;t.Height=36;});
            t.Key("ctrl+q");
            using var app=new EditorApp(folder,path,false,t);app.Run();
        });
        test("Tag search matches all tags case insensitively with upper-case extensions",()=>
        {
            string folder=Path.Combine(root,"tag-search");Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,"yes.MD"),"<!-- tags: WORK, project -->");File.WriteAllText(Path.Combine(folder,"no.txt"),"<!-- tags: work -->");
            if(DocumentTags.Find(folder,"work, PROJECT").Count!=1)throw new Exception("Tag query mismatch");
        });
    }
}
