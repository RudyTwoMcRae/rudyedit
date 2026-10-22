using System.Text.RegularExpressions;
namespace RudyEdit;
public sealed record Language(string Id, string Name, string[] Extensions, string Keywords, string Comment);
public static class Languages
{
    public static readonly Language[] All =
    [
        new("csharp","C#",[".cs",".csx"],"abstract async await bool break byte case catch char class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly record ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using var virtual void volatile while yield Console WriteLine Task List Dictionary","//"),
        new("c","C",[".c",".h"],"auto break case char const continue default do double else enum extern float for goto if inline int long register restrict return short signed sizeof static struct switch typedef union unsigned void volatile while printf scanf malloc free include define","//"),
        new("html","HTML",[".html",".htm"],"html head body main section article header footer nav div span p a img input button form label link meta script style table tr td ul ol li h1 h2 h3 class id href src alt type name value title role aria-label","<!--"),
        new("css","CSS",[".css",".scss",".sass",".less"],"color background display flex grid margin padding border width height font-family font-size font-weight align-items justify-content position relative absolute gap hover focus @media @import","/*"),
        new("javascript","JavaScript",[".js",".mjs",".cjs",".ts"],"async await break case catch class const continue debugger default delete do else export extends false finally for from function if import in instanceof let new null of return static super switch this throw true try typeof undefined var void while yield console log Promise fetch document window querySelector addEventListener","//"),
        new("php","PHP / CodeIgniter",[".php"],"abstract array as break callable case catch class clone const continue declare default die do echo else elseif empty endfor endforeach endif endwhile eval exit extends final finally fn for foreach function global if implements include include_once instanceof interface isset list namespace new null parent print private protected public readonly require require_once return self static switch throw trait try unset use var while yield Controller Model view redirect route service","//"),
        new("react","React / Next.js",[".jsx",".tsx"],"import export default function return const let async await useState useEffect useMemo useCallback useRef useContext createContext Fragment Suspense children className onClick onChange props state React Link Image useRouter generateMetadata getServerSideProps","//"),
        new("vue","Vue / Nuxt",[".vue"],"template script setup style ref reactive computed watch watchEffect defineProps defineEmits onMounted v-if v-else v-for v-model v-bind v-on class scoped useFetch useAsyncData definePageMeta NuxtLink NuxtPage","//"),
        new("python","Python",[".py",".pyw"],"False None True and as assert async await break class continue def del elif else except finally for from global if import in is lambda nonlocal not or pass raise return try while with yield print input len range enumerate zip str int float list dict set tuple open sum sorted __name__ __main__","#"),
        new("prisma","Prisma ORM",[".prisma"],"datasource generator model enum type provider url env String Int Float Boolean DateTime Json Bytes Decimal BigInt Unsupported @id @default @unique @relation @map @@index @@unique @@map autoincrement now uuid cuid","//"),
        new("rust","Rust",[".rs"],"as async await break const continue crate dyn else enum extern false fn for if impl in let loop match mod move mut pub ref return self Self static struct super trait true type unsafe use where while println vec Some None Ok Err String Vec Option Result","//"),
        new("xml","XML",[".xml",".xaml",".csproj",".props",".targets",".svg"],"xml xmlns version encoding true false Include Update Remove Condition PropertyGroup ItemGroup Project Target Binding Name Keys Context Description","<!--"),
        new("markdown","Markdown",[".md",".markdown",".txt"],"",""),
        new("json","JSON",[".json",".jsonc"],"true false null","//")
    ];
    public static string Detect(string? path) => All.FirstOrDefault(l=>l.Extensions.Contains(Path.GetExtension(path??"").ToLowerInvariant()))?.Id ?? "text";
    public static Language Get(string id) => All.FirstOrDefault(l=>l.Id==id) ?? new("text","Plain text",[],"","");
    public static List<string> Complete(Document d)
    {
        string word=d.WordAtCursor();
        string extra = d.Language is "html" or "css" or "react" or "vue" ? "flex grid block hidden inline-flex items-center justify-between justify-center gap-4 p-4 px-4 py-2 mx-auto max-w-lg rounded rounded-lg border bg-blue-600 text-white text-sm font-bold shadow hover:bg-blue-700 md:grid-cols-2" : "";
        return (Get(d.Language).Keywords + " " + extra).Split(' ',StringSplitOptions.RemoveEmptyEntries)
            .Concat(Regex.Matches(d.Text,"[A-Za-z_$][A-Za-z0-9_$-]{2,}").Select(m=>m.Value))
            .Where(s=>s.Length>word.Length&&s.StartsWith(word,StringComparison.OrdinalIgnoreCase)).Distinct().Order().Take(100).ToList();
    }
    public static (string Color,int Style)[] Highlight(string text,string language)
    {
        var result=Enumerable.Repeat(("#d8dee9",0),text.Length).ToArray();
        void Mark(int a,int n,string c,int style=0) { for(int i=a;i<Math.Min(text.Length,a+n);i++)result[i]=(c,style); }
        foreach(Match m in Regex.Matches(text,@"\b\d+(?:\.\d+)?\b")) Mark(m.Index,m.Length,"#d9a9ef");
        var words=Get(language).Keywords.Split(' ').ToHashSet();
        foreach(Match m in Regex.Matches(text,@"[\w@-]+")) if(words.Contains(m.Value))Mark(m.Index,m.Length,"#80c9ff",1);
        if(language is "html" or "xml" or "vue" or "react") foreach(Match m in Regex.Matches(text,"</?[A-Za-z][^>]*>"))Mark(m.Index,m.Length,"#82d4b3");
        foreach(Match m in Regex.Matches(text,"\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'"))Mark(m.Index,m.Length,"#a3d89b");
        string comment=Get(language).Comment; int pos=comment.Length>0?text.IndexOf(comment,StringComparison.Ordinal):-1;
        if(pos>=0&&result[pos].Item1!="#a3d89b")Mark(pos,text.Length-pos,"#788ca6",2);
        if(language=="markdown")
        {
            if(text.StartsWith('#'))Mark(0,text.Length,"#e6bc78",1);
            foreach(Match m in Regex.Matches(text,@"\*\*[^*]+\*\*"))Mark(m.Index,m.Length,"#eff5ff",1);
            foreach(Match m in Regex.Matches(text,@"`[^`]+`"))Mark(m.Index,m.Length,"#a3d89b");
        }
        return result;
    }
}
public sealed class SpellChecker
{
    readonly HashSet<string> words=new(StringComparer.OrdinalIgnoreCase);
    readonly string custom=Path.Combine(Settings.ConfigDirectory,"dictionary.txt");
    public bool Available=>words.Count>0;
    public SpellChecker()
    {
        foreach(var file in new[]{"/usr/share/hunspell/en_US.dic","/usr/share/dict/words",Path.Combine(AppContext.BaseDirectory,"assets","words.txt"),custom})
            if(File.Exists(file))foreach(var line in File.ReadLines(file)) { string word=line.Split('/')[0].Trim();if(word.Length>1&&!char.IsDigit(word[0]))words.Add(word); }
        foreach(var w in "a I RudyEdit Markdown Neovim Ctrl Alt Shift VS VIM Csharp JavaScript TypeScript CodeIgniter Prisma Nuxt Tailwind React Vue Python Rust HTML CSS XML Joplin".Split(' '))words.Add(w);
    }
    public IEnumerable<(int Index,string Word)> Unknown(string text) => Regex.Matches(text,@"\b[A-Za-z][A-Za-z']*\b").Where(m=>m.Length>1&&!words.Contains(m.Value)).Select(m=>(m.Index,m.Value));
    public void Add(string word) { words.Add(word);File.AppendAllText(custom,word+Environment.NewLine); }
    public List<string> Suggest(string word) => words.Where(w=>Math.Abs(w.Length-word.Length)<=2&&char.ToLowerInvariant(w[0])==char.ToLowerInvariant(word[0])).Select(w=>(Word:w,Distance:Distance(w.ToLowerInvariant(),word.ToLowerInvariant()))).Where(x=>x.Distance<=2).OrderBy(x=>x.Distance).ThenBy(x=>x.Word).Take(12).Select(x=>x.Word).ToList();
    static int Distance(string a,string b)
    {
        int[] prev=Enumerable.Range(0,b.Length+1).ToArray();
        for(int i=1;i<=a.Length;i++){var row=new int[b.Length+1];row[0]=i;for(int j=1;j<=b.Length;j++)row[j]=Math.Min(Math.Min(row[j-1]+1,prev[j]+1),prev[j-1]+(a[i-1]==b[j-1]?0:1));prev=row;}return prev[b.Length];
    }
}
public sealed class FileBrowser
{
    public string Folder {get;private set;} public string Filter {get;set;}="";public int Index{get;set;} public int Scroll{get;set;}
    public List<string> Entries{get;private set;}=[];
    public string TagFilter {get;set;}="";
    public string? Selected=>Entries.ElementAtOrDefault(Index);
    public FileBrowser(string folder){Folder=Path.GetFullPath(folder);Refresh();}
    public void Load(string folder){Folder=Path.GetFullPath(folder);Filter=TagFilter="";Index=Scroll=0;Refresh();}
    public void Refresh()
    {
        if(TagFilter.Length>0){Entries=DocumentTags.Find(Folder,TagFilter);Index=Math.Clamp(Index,0,Math.Max(0,Entries.Count-1));return;}
        var d=new DirectoryInfo(Folder);Entries= d.EnumerateFileSystemInfos().Where(x=>x.Name.Contains(Filter,StringComparison.OrdinalIgnoreCase)).OrderBy(x=>x is DirectoryInfo?0:1).ThenBy(x=>x.Name).Select(x=>x.FullName).ToList();
        if(d.Parent!=null)Entries.Insert(0,d.Parent.FullName);Index=Math.Clamp(Index,0,Math.Max(0,Entries.Count-1));
    }
    public static void CopyPath(string from,string to,bool move)
    {
        from=Path.GetFullPath(from);to=Path.GetFullPath(to);
        if(File.Exists(to)||Directory.Exists(to))throw new IOException("Destination already exists; nothing overwritten.");
        if(Directory.Exists(from))
        {
            if(to.StartsWith(from+Path.DirectorySeparatorChar,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))throw new IOException("Cannot paste a folder inside itself.");
            var info=new DirectoryInfo(from); if(info.LinkTarget!=null)throw new IOException("Copying symbolic-link directories is not supported.");
            if(move)Directory.Move(from,to);else CopyDirectory(from,to);
        }
        else if(move)File.Move(from,to);else File.Copy(from,to);
    }
    static void CopyDirectory(string from,string to)
    {
        // Validate the entire tree before creating any destination.
        var dirs=Directory.GetDirectories(from,"*",new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint});
        var files=Directory.GetFiles(from,"*",new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint});
        Directory.CreateDirectory(to);foreach(var dir in dirs)Directory.CreateDirectory(Path.Combine(to,Path.GetRelativePath(from,dir)));foreach(var file in files)File.Copy(file,Path.Combine(to,Path.GetRelativePath(from,file)));
    }
}
