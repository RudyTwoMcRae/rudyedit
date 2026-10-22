using System.Text;
using System.Xml.Linq;
using RudyEdit;

static class TagStoreTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Throws(Action action){try{action();}catch(IOException){return;}throw new Exception("Expected storage conflict");}
    public static void Run(Action<string,Action> test,string root)
    {
        test("Tag registry round trip: names only in XML, hidden id, UTF-8 CRLF BOM, editor text intact",()=>
        {
            string path=Path.Combine(root,"registry.txt");File.WriteAllText(path,"hello\r\nworld\r\n",new UTF8Encoding(true));
            var d=Document.Open(path);d.Cursor=4;d.Anchor=1;DocumentTags.Set(d,["Work","work","a&b", "<private>"]);
            Check(d.Dirty&&d.Cursor==4&&d.Anchor==1&&d.Text=="hello\nworld\n","Tag change altered content or selection");
            d.Save();string raw=File.ReadAllText(path);
            Check(!raw.Contains("Work")&&!raw.Contains("private")&&raw.Contains("rudyedit-id:"),"Names leaked into text file");
            Check(raw.Contains("\r\n")&&File.ReadAllBytes(path).AsSpan().StartsWith(new byte[]{239,187,191}),"Encoding changed");
            Check(TagStore.Read(d.TagId!).SequenceEqual(new[]{"Work","a&b","<private>"}),"Registry tags differ");
            var again=Document.Open(path);Check(again.Text==d.Text&&!again.Dirty&&again.TagId==d.TagId&&DocumentTags.Read(again).Length==3,"Reopen mismatch");
            DocumentTags.Set(again,[]);Check(again.Dirty,"Removing tags not dirty");again.Undo();Check(!again.Dirty&&DocumentTags.Read(again).Length==3,"Tag undo mismatch");again.Redo();again.Save();Check(DocumentTags.Read(Document.Open(path)).Length==0,"Tags not removed");
        });
        test("Tag-only unsaved changes discard without changing Tags.xml",()=>
        {
            string before=File.ReadAllText(Settings.TagsPath);var d=new Document();DocumentTags.Set(d,["not saved"]);
            Check(d.Dirty&&d.Text=="","Tag change not staged separately");d.Undo();Check(!d.Dirty&&d.TagId==null,"Undo did not restore blank state");
            Check(File.ReadAllText(Settings.TagsPath)==before,"Unsaved tags persisted");
        });
        test("Legacy header migrates on save without stripping examples or body blank lines",()=>
        {
            string path=Path.Combine(root,"legacy.md");File.WriteAllText(path,"<!-- tags: old, OLD, legacy -->\n\n\nbody\n");
            var d=Document.Open(path);Check(d.Dirty&&d.Text=="\nbody\n"&&DocumentTags.Read(d).Length==2,"Migration staging failed");d.Save();
            Check(!File.ReadAllText(path).Contains("legacy")&&DocumentTags.Read(Document.Open(path)).Contains("legacy"),"Migration lost tags");
            string sample=Path.Combine(root,"example.md");File.WriteAllText(sample,"Example:\n```markdown\n<!-- tags: work -->\n```\n");
            Check(Document.Open(sample).Text==File.ReadAllText(sample),"Example comment stripped");
        });
        test("Identifier survives external rename; Save As creates independent tags",()=>
        {
            string a=Path.Combine(root,"original.md"),b=Path.Combine(root,"copy.md"),renamed=Path.Combine(root,"renamed.md");
            var d=new Document();d.Insert("body");DocumentTags.Set(d,["first"]);d.Save(a);string? first=d.TagId;DocumentTags.Set(d,["before copy"]);d.Save(b);
            Check(d.TagId!=first,"Save As shared identity");d.Undo();d.Save();Check(DocumentTags.Read(d).SequenceEqual(new[]{"first"}),"Save As undo altered identity");DocumentTags.Set(d,["second"]);d.Save();File.Move(a,renamed);
            Check(DocumentTags.Read(Document.Open(renamed)).SequenceEqual(new[]{"first"}),"Rename or copy overwrote tags");
            Check(DocumentTags.Find(root,"second").Contains(b),"XML-backed search failed");
        });
        test("Registry merges independent saves and refuses stale edits on the same id",()=>
        {
            string path=Path.Combine(root,"concurrent.md");var d=new Document();DocumentTags.Set(d,["initial"]);d.Save(path);
            var first=Document.Open(path);var second=Document.Open(path);DocumentTags.Set(first,["changed"]);first.Save();DocumentTags.Set(second,["stale"]);
            Throws(()=>second.Save());Check(second.Dirty&&TagStore.Read(first.TagId!).SequenceEqual(new[]{"changed"}),"Stale write replaced tags");
            var other=new Document();DocumentTags.Set(other,["independent"]);other.Save(Path.Combine(root,"independent.md"));
            Check(TagStore.Read(first.TagId!).Contains("changed"),"Unrelated tag record lost");
        });
        test("Malformed registry is preserved and cannot partially save a tagged document",()=>
        {
            string original=File.ReadAllText(Settings.TagsPath),path=Path.Combine(root,"storage-failure.md");File.WriteAllText(path,"keep");
            try
            {
                File.WriteAllText(Settings.TagsPath,"<broken");var d=Document.Open(path);DocumentTags.Set(d,["new"]);Throws(()=>d.Save());
                Check(File.ReadAllText(path)=="keep"&&File.ReadAllText(Settings.TagsPath)=="<broken"&&d.Dirty,"Failed save modified files");
            }
            finally{File.WriteAllText(Settings.TagsPath,original);}
        });
        test("Failed document replacement rolls the XML transaction back",()=>
        {
            string original=File.ReadAllText(Settings.TagsPath),id=Guid.NewGuid().ToString("D");
            Throws(()=>TagStore.Commit(id,["rollback"],[],"missing",()=>throw new IOException("simulated replacement failure")));
            Check(XNode.DeepEquals(XDocument.Parse(original),XDocument.Load(Settings.TagsPath)),"Registry was not restored");
        });
        test("The registry cannot tag itself or be replaced by a tagged document",()=>
        {
            string original=File.ReadAllText(Settings.TagsPath);var registry=Document.Open(Settings.TagsPath);bool rejected=false;
            try{DocumentTags.Set(registry,["self"]);}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"Registry was allowed to tag itself");
            var d=new Document();d.Insert(original);DocumentTags.Set(d,["self"]);rejected=false;
            try{d.Save(Settings.TagsPath,true);}catch(InvalidOperationException){rejected=true;}
            Check(rejected&&File.ReadAllText(Settings.TagsPath)==original,"Tagged Save As replaced registry");
        });
        test("Tags.xml validation rejects duplicate IDs and DTDs; tag names are XML escaped",()=>
        {
            string id=Guid.NewGuid().ToString("D");
            foreach(string bad in new[]{"<Tags />",$"<Tags Version=\"1\"><File Id=\"{id}\"/><File Id=\"{id}\"/></Tags>","<!DOCTYPE Tags [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><Tags Version=\"1\" />"})
            {
                bool failed=false;try{TagStore.Validate(bad);}catch{failed=true;}Check(failed,"Invalid registry accepted");
            }
            TagStore.Validate(File.ReadAllText(Settings.TagsPath));
        });
    }
}
