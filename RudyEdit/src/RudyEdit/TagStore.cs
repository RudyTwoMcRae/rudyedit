using System.Xml;
using System.Xml.Linq;
namespace RudyEdit;

/// <summary>Per-user tag registry. Writers reload and merge under a cross-process lock.</summary>
public sealed class TagStorageException(string message,Exception inner):IOException(message,inner);

public static class TagStore
{
    public static bool IsRegistryPath(string? path)=>path!=null&&string.Equals(Path.GetFullPath(path),Path.GetFullPath(Settings.TagsPath),OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);
    static XDocument Parse(XmlReader reader)
    {
        var xml=XDocument.Load(reader,LoadOptions.PreserveWhitespace);
        if(xml.Root?.Name!="Tags"||(string?)xml.Root.Attribute("Version")!="1")throw new FormatException("Tags.xml must have a <Tags Version=\"1\"> root.");
        var ids=new HashSet<Guid>();
        foreach(var file in xml.Root.Elements())
        {
            if(file.Name!="File"||!Guid.TryParse((string?)file.Attribute("Id"),out var id)||!ids.Add(id))throw new FormatException("Tags.xml has an invalid or duplicate File Id.");
            if(file.Elements().Any(t=>t.Name!="Tag"||t.HasElements||string.IsNullOrWhiteSpace(t.Value)))throw new FormatException("Tags.xml File entries may contain nonempty <Tag> values only.");
        }
        return xml;
    }
    static XmlReaderSettings ReaderSettings()=>new(){DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null};
    public static void Validate(string text){using var reader=XmlReader.Create(new StringReader(text),ReaderSettings());Parse(reader);}
    static XDocument Load()
    {
        if(!File.Exists(Settings.TagsPath))return new(new XElement("Tags",new XAttribute("Version","1")));
        using var reader=XmlReader.Create(Settings.TagsPath,ReaderSettings());return Parse(reader);
    }
    static string[] Values(XElement? file)=>DocumentTags.Normalize(string.Join(",",file?.Elements("Tag").Select(t=>t.Value)??[]));
    public static Dictionary<string,string[]> ReadAll()=>Load().Root!.Elements("File").ToDictionary(f=>Guid.Parse((string)f.Attribute("Id")!).ToString("D"),Values,StringComparer.OrdinalIgnoreCase);
    public static string[] Read(string id)=>ReadAll().GetValueOrDefault(id,[]);
    static FileStream AcquireLock()
    {
        Directory.CreateDirectory(Settings.ConfigDirectory);
        for(int n=0;;n++)
        {
            try{return new FileStream(Settings.TagsPath+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
            catch(IOException) when(n<40){Thread.Sleep(25);}
        }
    }
    static void Write(XDocument xml)
    {
        string tmp=Settings.TagsPath+".save-"+Guid.NewGuid().ToString("N");
        try{xml.Save(tmp);File.Move(tmp,Settings.TagsPath,true);}
        finally{if(File.Exists(tmp))File.Delete(tmp);}
    }
    // Commit metadata before replacing the document. A failed document write restores
    // the registry while the lock is held; a registry failure leaves the file untouched.
    public static void Commit(string id,string[] tags,string[] expected,string path,Action writeDocument)
    {
        try{CommitCore(id,tags,expected,path,writeDocument);}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or XmlException or FormatException)
        {throw new TagStorageException("Tags.xml: "+ex.Message,ex);}
    }
    static void CommitCore(string id,string[] tags,string[] expected,string path,Action writeDocument)
    {
        using var gate=AcquireLock();var xml=Load();var original=new XDocument(xml);
        var file=xml.Root!.Elements("File").FirstOrDefault(f=>Guid.Parse((string)f.Attribute("Id")!).ToString("D")==id);
        if(!Values(file).SequenceEqual(expected))throw new IOException("Tags changed outside this document. Reopen the file before saving its tags.");
        if(file==null){file=new XElement("File",new XAttribute("Id",id));xml.Root.Add(file);}
        file.SetAttributeValue("Path",path);file.ReplaceNodes(tags.Select(t=>new XElement("Tag",t)));
        Write(xml);
        try{writeDocument();}
        catch(Exception failure)
        {
            try{Write(original);}
            catch(Exception rollback){throw new IOException("Saving the document failed and Tags.xml could not be restored. Check both files before retrying.",new AggregateException(failure,rollback));}
            throw;
        }
    }
}
