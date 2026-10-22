using System.Text.RegularExpressions;
namespace RudyEdit;

/// <summary>Tags live in Tags.xml; only an opaque identifier is embedded in saved files.</summary>
public static class DocumentTags
{
    static readonly Regex Identifier=new(@"\A<!-- rudyedit-id: ([0-9a-fA-F-]{36}) -->\n?",RegexOptions.CultureInvariant);
    static readonly Regex Legacy=new(@"\A<!--\s*tags:\s*(.*?)\s*-->(?:\n\n?)?",RegexOptions.IgnoreCase|RegexOptions.Singleline);
    public static string[] Normalize(string text)=>text.Split(',',StringSplitOptions.RemoveEmptyEntries)
        .Select(t=>Regex.Replace(t,@"\s+"," ").Trim()).Where(t=>t.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public static string[] Read(Document doc)=>doc.TagValues.ToArray();
    public static string? FindId(string text)
    {
        var m=Identifier.Match(text);return m.Success&&Guid.TryParse(m.Groups[1].Value,out var id)?id.ToString("D"):null;
    }
    internal static (string Text,string? Id,string[] Tags,bool Migrate) Decode(string text)
    {
        string? id=FindId(text);var legacy=Legacy.Matches(text);
        string[] tags=Normalize(string.Join(",",(id==null?[]:TagStore.Read(id)).Concat(legacy.Select(m=>m.Groups[1].Value))));
        string body=Identifier.Replace(text,m=>Guid.TryParse(m.Groups[1].Value,out _)?"":m.Value);
        body=Legacy.Replace(body,"");
        return(body,id??(tags.Length>0?Guid.NewGuid().ToString("D"):null),tags,legacy.Count>0);
    }
    internal static string Encode(string text,string? id)=>id==null?text:$"<!-- rudyedit-id: {id} -->\n"+text;
    public static void Set(Document doc,IEnumerable<string> tags)
    {
        if(TagStore.IsRegistryPath(doc.FilePath))throw new InvalidOperationException("Tags.xml is the tag registry and cannot itself be tagged.");
        doc.SetTags(Normalize(string.Join(",",tags)));
    }
    public static List<string> Find(string folder,string query)
    {
        var wanted=Normalize(query);if(wanted.Length==0)return [];
        var registry=TagStore.ReadAll();var result=new List<string>();
        var opts=new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint,IgnoreInaccessible=true};
        foreach(string path in Directory.EnumerateFiles(folder,"*",opts).Take(10000))
        {
            if(!new[]{".md",".txt",".markdown"}.Contains(Path.GetExtension(path),StringComparer.OrdinalIgnoreCase))continue;
            try
            {
                if(new FileInfo(path).Length>2_000_000)continue;
                string text=File.ReadAllText(path);string? id=FindId(text.Replace("\r\n","\n"));
                var present=Normalize(string.Join(",",(id==null?[]:registry.GetValueOrDefault(id,[])).Concat(Legacy.Matches(text).Select(m=>m.Groups[1].Value))));
                if(wanted.All(t=>present.Contains(t,StringComparer.OrdinalIgnoreCase)))result.Add(path);
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){ErrorLog.Write(ex,"Tag search: "+path);}
        }
        return result.OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ToList();
    }
}
