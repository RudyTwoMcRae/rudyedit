using System.Globalization;
using System.Runtime.CompilerServices;
namespace RudyEdit;
public sealed partial class EditorApp
{
    // Drafts belong to documents, so changing tabs cannot attach a tag to another file.
    sealed class TagEntry {public string Value="";}
    readonly ConditionalWeakTable<Document,TagEntry> tagDrafts=new();
    bool showTagBar=true;
    string barFocus="",tagQuery="";int barCursor,formatIndex;
    readonly List<(Rect Bounds,string Command)> textBarHits=[];
    Rect tagInputBounds,tagListBounds,tagSearchBounds;
    readonly string[] formatCommands=["font","font-size","bold","italic","underline","foreground","background","bullets","ordered","spell","toggle-spell","preview"];
    string TagDraft {get=>tagDrafts.GetValue(Doc,_=>new TagEntry()).Value;set=>tagDrafts.GetValue(Doc,_=>new TagEntry()).Value=value;}
    void FocusBar(string focus)
    {
        barFocus=focus;filesFocus=consoleFocus=vimCommand=false;chord="";
        if(focus=="search")showFiles=true;
        if(focus=="tags")showTagBar=true;
        barCursor=(focus=="tags"?TagDraft:tagQuery).Length;
    }
    bool HandleTextBars(KeyEvent k)
    {
        if(mode!="Text"){barFocus="";return false;}
        if(!k.Paste)
        {
            if(k.Key=="ctrl+alt+t"){FocusBar("tags");return true;}
            if(k.Key=="ctrl+alt+g"){FocusBar("search");return true;}
            if(k.Key=="ctrl+alt+m"){FocusBar("format");return true;}
        }
        if(barFocus.Length==0)return false;
        string lastKey=k.Key.Split('+')[^1];
        if(!k.Paste&&lastKey.StartsWith('f')&&int.TryParse(lastKey.AsSpan(1),out _)){barFocus="";return false;}
        if(k.Key=="esc"){barFocus="";return true;}
        if(barFocus=="format")
        {
            if(k.Key is "right" or "tab")formatIndex=(formatIndex+1)%formatCommands.Length;
            else if(k.Key is "left" or "shift+tab")formatIndex=(formatIndex+formatCommands.Length-1)%formatCommands.Length;
            else if(k.Key is "enter" or "space")RunFormat(formatCommands[formatIndex]);
            else if(k.Key.StartsWith("ctrl+")){barFocus="";return false;}
            return true;
        }
        bool tags=barFocus=="tags";string value=tags?TagDraft:tagQuery;
        barCursor=Math.Clamp(barCursor,0,value.Length);
        if(k.Key=="enter")
        {
            if(tags)
            {
                DocumentTags.Set(Doc,DocumentTags.Read(Doc).Concat(DocumentTags.Normalize(value)));
                TagDraft="";barCursor=0;Changed();message="Tags added — Ctrl+S saves Tags.xml and the file link.";
            }
            else{browser.TagFilter=value.Trim();browser.Index=browser.Scroll=0;browser.Refresh();barFocus="";filesFocus=true;message=browser.TagFilter.Length==0?"Tag filter cleared.":$"{browser.Entries.Count} files match all tags: {browser.TagFilter} (saved files)";}
            return true;
        }
        if(k.Key=="tab"){FocusBar(tags?"search":"tags");return true;}
        if(k.Key=="ctrl+a"){value="";barCursor=0;}
        else if(k.Key=="home")barCursor=0;
        else if(k.Key=="end")barCursor=value.Length;
        else if(k.Key=="left")barCursor=StringInfo.ParseCombiningCharacters(value[..barCursor]).LastOrDefault();
        else if(k.Key=="right"&&barCursor<value.Length)barCursor+=StringInfo.GetNextTextElementLength(value,barCursor);
        else if(k.Key=="backspace"&&barCursor>0){int start=StringInfo.ParseCombiningCharacters(value[..barCursor]).LastOrDefault();value=value.Remove(start,barCursor-start);barCursor=start;}
        else if(k.Key=="delete"&&barCursor<value.Length)value=value.Remove(barCursor,StringInfo.GetNextTextElementLength(value,barCursor));
        else if(k.Paste||k.Text.Length>0&&!k.Key.StartsWith("ctrl+")&&!k.Key.StartsWith("alt+"))
        {string insert=k.Text.Replace('\r',' ').Replace('\n',' ').Replace('\t',' ');value=value.Insert(barCursor,insert);barCursor+=insert.Length;}
        else if(k.Key.StartsWith("ctrl+")){barFocus="";return false;}
        if(tags)TagDraft=value;else tagQuery=value;
        return true;
    }
    void RunFormat(string command)
    {
        barFocus="";
        if(command=="font-size")
        {Choose("Font size",new[]{8,9,10,11,12,14,16,18,20,24,28,32,36,48,72,96}.Select(n=>new Choice(n+" pt",n.ToString())).Append(new("Custom…","custom")).ToList(),v=>{if(v=="custom")Execute(command);else{Doc.Wrap($"<span style=\"font-size:{v}pt\">","</span>");Changed();}});}
        else if(command is "foreground" or "background")
        {
            Choose(command=="foreground"?"Text color":"Background color",new[]{"#d8dee9","#ffffff","#ff7777","#ffcc66","#82d4b3","#80c9ff","#d9a9ef","#151c28","#000000"}.Select(c=>new Choice(c,c)).Append(new("Custom…","custom")).ToList(),v=>
            {if(v=="custom")Execute(command);else if(command=="background")background=v;else{foreground=v;Doc.Wrap("<span style=\"color:"+v+"\">","</span>");Changed();}});
        }
        else Execute(command);
        if(popup!=null&&!popup.Prompt&&popup.Content==null)popup.Dropdown=true;
    }
    void DrawTextBars(Screen s,int fileW,int bottom,int searchY,ref int cx,ref int cy)
    {
        textBarHits.Clear();tagInputBounds=tagListBounds=tagSearchBounds=default;
        if(mode!="Text")return;
        const string bg="#283950";
        s.Fill(new(0,1,width,1),bg);
        int x=0;
        void Button(string label,string command,bool clip=false)
        {
            var icon=UiIcons.Command(command);
            if(settings.UseNerdIcons)
            {
                if(command is "bold" or "italic" or "underline")label=icon.Glyph;
                else{label=command switch{"background"=>"Fill ▾","spell"=>"Spell","toggle-spell"=>"Auto",_=>label};label=icon.Glyph+" "+label;}
            }
            int w=Screen.DisplayWidth(label)+2;if(x+w>width){if(!clip||width-x<3)return;w=width-x;}
            var rect=new Rect(x,1,w,1);textBarHits.Add((rect,command));
            bool focused=barFocus=="format"&&Array.IndexOf(formatCommands,command)==formatIndex;
            s.Put(x,1," "+label+" ","#ffffff",focused?"#527396":bg,focused?4:0,w);x+=w;
        }
        // Format always exposes the complete toolbar when the terminal is narrow.
        Button("Format ▾","format-menu");
        if(barFocus=="format"&&width<110)
        {
            string id=formatCommands[formatIndex];Button(Commands.All.First(c=>c.Id==id).Name+(id is "font" or "font-size"?" ▾":""),id,true);
        }
        else
        {
            Button("Font ▾","font");Button("Size ▾","font-size");Button("B","bold");Button("I","italic");Button("U","underline");
            Button("Color ▾","foreground");Button("Background ▾","background");Button("• List","bullets");Button("1. List","ordered");Button("Spelling","spell");Button("Auto spell","toggle-spell");Button("Preview","preview");
        }
        if(showTagBar)
        {
            s.Fill(new(0,bottom,width,2),bg);
            tagListBounds=new(0,bottom,width,1);
            var tags=DocumentTags.Read(Doc);string all=tags.Length==0?"(none)":string.Join(", ",tags);
            s.Put(1,bottom,"Tags: "+all+"  [click to view/edit all]","#a9d5f5",bg,0,width-2);
            s.Put(1,bottom+1,"Add tags: ","#ffffff",bg,0,width-2);
            tagInputBounds=new(Math.Min(11,width-2),bottom+1,Math.Max(1,width-12),1);
            DrawBarInput(s,tagInputBounds,TagDraft,"tags","comma-separated • Enter add",ref cx,ref cy);
        }
        if(showFiles)
        {
            int y=searchY;s.Fill(new(0,y,fileW-1,1),bg);
            s.Put(0,y,"Tag? ","#ffffff",bg,0,fileW-1);
            tagSearchBounds=new(Math.Min(5,fileW-2),y,Math.Max(1,fileW-7),1);
            DrawBarInput(s,tagSearchBounds,tagQuery,"search","Enter search",ref cx,ref cy);
            var clear=new Rect(fileW-2,y,1,1);textBarHits.Add((clear,"clear-tag-search"));s.Put(clear.X,y,"×","#ffffff",bg);
        }
    }
    void DrawBarInput(Screen s,Rect r,string value,string focus,string hint,ref int cx,ref int cy)
    {
        bool selected=barFocus==focus;int cursor=Math.Clamp(barCursor,0,value.Length);
        int start=selected?Math.Max(0,cursor-Math.Max(1,r.W-2)):0;
        if(start>0&&char.IsLowSurrogate(value[start]))start--;
        s.Fill(r,selected?"#3b5678":"#202c3e");s.Put(r.X,r.Y,value.Length==0&&!selected?hint:value[start..],"#dcecff",selected?"#3b5678":"#202c3e",0,r.W);
        if(selected){cx=Math.Min(r.X+r.W-1,r.X+Screen.DisplayWidth(value[start..cursor]));cy=r.Y;}
    }
    bool TextBarMouse(MouseEvent mouse)
    {
        if(mode!="Text"||mouse.Action!=MouseAction.Press||mouse.Button!=0)return false;
        if(showTagBar&&mouse.Y==tagInputBounds.Y&&mouse.X>=0&&mouse.X<width){FocusBar("tags");return true;}
        if(showTagBar&&Contains(tagListBounds,mouse.X,mouse.Y)){EditTags();return true;}
        if(showFiles&&mouse.Y==tagSearchBounds.Y&&mouse.X>=0&&mouse.X<tagSearchBounds.X+tagSearchBounds.W){FocusBar("search");return true;}
        foreach(var hit in textBarHits)if(Contains(hit.Bounds,mouse.X,mouse.Y))
        {
            if(hit.Command=="clear-tag-search"){tagQuery=browser.TagFilter="";browser.Index=browser.Scroll=0;browser.Refresh();barFocus="";message="Tag filter cleared.";}
            else if(hit.Command=="format-menu"){Choose("Formatting",formatCommands.Select(id=>{var c=Commands.All.First(c=>c.Id==id);return new Choice(c.Name,id);}).ToList(),RunFormat);popup!.Dropdown=true;}
            else RunFormat(hit.Command);
            return true;
        }
        barFocus="";return false;
    }
}
