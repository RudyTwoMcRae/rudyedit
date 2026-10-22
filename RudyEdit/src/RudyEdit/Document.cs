using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace RudyEdit;
public readonly record struct Selection(int Start, int Length);
public sealed class Document
{
    public string Text { get; private set; } = "";
    public string? FilePath { get; private set; }
    public string Name => FilePath == null ? "Untitled.md" : Path.GetFileName(FilePath);
    public string Language => Languages.Detect(FilePath);
    public bool Dirty => Text != saved || TagId != savedTagId || !TagValues.SequenceEqual(savedTags) || migrateTags;
    public string? TagId { get; private set; }
    internal string[] TagValues { get; private set; } = [];
    string? savedTagId;
    string[] savedTags = [];
    bool migrateTags;
    sealed record State(string Text,int Cursor,string? Id,string[] Tags,bool Migrate);
    State Snapshot()=>new(Text,Cursor,TagId,TagValues,migrateTags);
    static void RebaseIdentity(Stack<State> history,string before,string? after)
    {
        var entries=history.Reverse().Select(s=>s.Id==before?s with {Id=after}:s).ToArray();history.Clear();foreach(var state in entries)history.Push(state);
    }
    void Restore(State state){Text=state.Text;Cursor=state.Cursor;TagId=state.Id;TagValues=state.Tags;migrateTags=state.Migrate;Anchor=null;}
    internal void SetTags(string[] tags)
    {
        if(TagValues.SequenceEqual(tags))return;
        undo.Push(Snapshot());redo.Clear();TagValues=tags;
        if(tags.Length>0)TagId??=Guid.NewGuid().ToString("D");
    }
    public int Cursor { get; set; }
    public int? Anchor { get; set; }
    public Selection Selection => new(Math.Min(Anchor ?? Cursor, Cursor), Math.Abs((Anchor ?? Cursor) - Cursor));
    public string NewLine { get; private set; } = Environment.NewLine;
    string saved = "";
    byte[]? diskHash;
    bool bom;
    readonly Stack<State> undo = new(), redo = new();
    string? linesFor; string[] cachedLines = [""];
    public string[] Lines { get { if (!ReferenceEquals(linesFor, Text)) { cachedLines = Text.Split('\n'); linesFor = Text; } return cachedLines; } }
    public (int Row, int Column) Position
    {
        get { int pos = Math.Clamp(Cursor, 0, Text.Length), line = Text.LastIndexOf('\n', Math.Max(0, pos - 1), pos); return (Text.AsSpan(0, pos).Count('\n'), pos - line - 1); }
    }
    public static Document Open(string path)
    {
        path = Path.GetFullPath(path);
        var d = new Document { FilePath = path };
        if (!File.Exists(path)) return d;
        if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new IOException("RudyEdit currently supports text files up to 8 MiB.");
        var bytes = File.ReadAllBytes(path);
        if (bytes.Contains((byte)0)) throw new IOException("Binary or UTF-16 file. Convert it to UTF-8 before editing.");
        d.bom = bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF });
        string s = new UTF8Encoding(false, true).GetString(bytes.AsSpan(d.bom ? 3 : 0));
        d.NewLine = s.Contains("\r\n") ? "\r\n" : "\n";
        var metadata=DocumentTags.Decode(s.Replace("\r\n", "\n").Replace('\r', '\n'));
        d.Text=d.saved=metadata.Text;d.TagId=d.savedTagId=metadata.Id;
        d.TagValues=metadata.Tags;d.savedTags=metadata.Migrate&&metadata.Id!=null?TagStore.Read(metadata.Id):metadata.Tags;d.migrateTags=metadata.Migrate;
        d.diskHash = SHA256.HashData(bytes); return d;
    }
    public void Save(string? path = null, bool overwriteExternal = false)
    {
        var target = Path.GetFullPath(path ?? FilePath ?? throw new IOException("Choose Save As for a new file."));
        if (!overwriteExternal && File.Exists(target) &&
            (target != FilePath || diskHash == null || !SHA256.HashData(File.ReadAllBytes(target)).SequenceEqual(diskHash)))
            throw new IOException("The destination exists or changed outside RudyEdit. Use Save As with a new name, or explicitly confirm overwrite.");
        // Save As creates an independent tag association; external renames keep the embedded id.
        bool copy=FilePath!=null&&!string.Equals(target,FilePath,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);
        string? id=copy&&TagId!=null?Guid.NewGuid().ToString("D"):TagId;
        if(TagStore.IsRegistryPath(target))
        {
            if(id!=null)throw new InvalidOperationException("Tags.xml is reserved for the tag registry. Save this tagged document under another filename.");
            TagStore.Validate(Text);
        }
        var content = DocumentTags.Encode(Text,id).Replace("\n", NewLine);
        var encoding = new UTF8Encoding(bom);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(content)).ToArray();
        var tmp = target + ".rudyedit-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllBytes(tmp, bytes);
            if (!OperatingSystem.IsWindows() && File.Exists(target)) File.SetUnixFileMode(tmp, File.GetUnixFileMode(target));
            if(id==null)File.Move(tmp,target,true);
            else TagStore.Commit(id,TagValues,copy||id!=savedTagId?[]:savedTags,target,()=>File.Move(tmp,target,true));
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
        if(copy&&TagId!=null){RebaseIdentity(undo,TagId,id);RebaseIdentity(redo,TagId,id);}
        FilePath = target; saved = Text; TagId=savedTagId=id; savedTags=TagValues; migrateTags=false; diskHash = SHA256.HashData(bytes);
    }
    public void Replace(int start, int length, string value)
    {
        start = Math.Clamp(start, 0, Text.Length); length = Math.Clamp(length, 0, Text.Length - start);
        value = value.Replace("\r\n", "\n").Replace('\r', '\n');
        undo.Push(Snapshot()); redo.Clear();
        Text = Text.Remove(start, length).Insert(start, value); Cursor = start + value.Length; Anchor = null;
    }
    public void Insert(string value) { var s = Selection; Replace(s.Start, s.Length, value); }
    public void Undo() { if (undo.TryPop(out var s)) { redo.Push(Snapshot()); Restore(s); } }
    public void Redo() { if (redo.TryPop(out var s)) { undo.Push(Snapshot()); Restore(s); } }
    public void Backspace()
    {
        if (Selection.Length > 0) Insert("");
        else if (Cursor > 0) { var p = Previous(Cursor); Replace(p, Cursor - p, ""); }
    }
    public void Delete() { if (Selection.Length > 0) Insert(""); else if (Cursor < Text.Length) Replace(Cursor, Next(Cursor) - Cursor, ""); }
    public int Previous(int pos) { if (pos <= 0) return 0; return StringInfo.ParseCombiningCharacters(Text[..pos]).LastOrDefault(); }
    public int Next(int pos) => pos >= Text.Length ? Text.Length : pos + StringInfo.GetNextTextElementLength(Text, pos);
    public void Move(int dx, int dy, bool select)
    {
        if (select) Anchor ??= Cursor; else Anchor = null;
        if (dy != 0) { var p = Position; SetPosition(p.Row + dy, p.Column); }
        else if (dx < 0) Cursor = Previous(Cursor); else Cursor = Next(Cursor);
    }
    public void SetPosition(int row, int column)
    {
        var lines = Lines; row = Math.Clamp(row, 0, lines.Length - 1); column = Math.Clamp(column, 0, lines[row].Length);
        Cursor = lines.Take(row).Sum(x => x.Length + 1) + column;
        if (Cursor > 0 && Cursor < Text.Length && char.IsLowSurrogate(Text[Cursor])) Cursor--;
    }
    public string WordAtCursor()
    {
        int p = Cursor; while (p > 0 && (char.IsLetterOrDigit(Text[p - 1]) || Text[p - 1] is '_' or '-')) p--;
        return Text[p..Cursor];
    }
    public void Wrap(string before, string after)
    {
        var s = Selection; var content = Text.Substring(s.Start, s.Length); Replace(s.Start, s.Length, before + content + after);
        if (s.Length == 0) Cursor -= after.Length;
    }
    public void PrefixLines(string prefix, bool ordered = false)
    {
        var s = Selection; int start = Text.LastIndexOf('\n', Math.Max(0, s.Start - 1), s.Start) + 1;
        int end = Text.IndexOf('\n', s.Start + s.Length); if (end < 0) end = Text.Length;
        Replace(start, end - start, string.Join('\n', Text[start..end].Split('\n').Select((x, i) => (ordered ? $"{i + 1}. " : prefix) + x)));
    }
    public bool Find(string query, bool wrap = true)
    {
        if (query.Length == 0) return false;
        int p = Text.IndexOf(query, Math.Min(Text.Length, Cursor + 1), StringComparison.OrdinalIgnoreCase);
        if (p < 0 && wrap) p = Text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (p < 0) return false; Cursor = p + query.Length; Anchor = p; return true;
    }
}
