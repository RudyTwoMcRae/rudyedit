using System.Text;
using System.Text.RegularExpressions;
using System.Net;
namespace RudyEdit;
public sealed partial class EditorApp
{
    void Prompt(string title,string initial,Action<string,string> accept)=>popup=new(){Title=title,Text=initial,Prompt=true,Accept=accept};
    void Choose(string title,List<Choice> items,Action<string> accept)=>popup=new(){Title=title,Items=items,Accept=(value,_)=>accept(value)};
    void ShowText(string title,string content)=>popup=new(){Title=title,Content=content};
    string? PromptSync(string title,string initial)
    {
        string? result=null;Prompt(title,initial,(text,_)=>result=text);ModalLoop();return result;
    }
    string? ChooseSync(string title,List<Choice> choices)
    {
        string? result=null;Choose(title,choices,value=>result=value);ModalLoop();return result;
    }
    bool Confirm(string text,bool yesDefault)
    {
        var choices=new List<Choice>{new("No — cancel","no",text),new("Yes","yes",text)};if(yesDefault)choices.Reverse();
        return ChooseSync(text,choices)=="yes";
    }
    void ModalLoop()
    {
        while(popup!=null&&!quit){Draw();var key=terminal.ReadKey();if(key.HasValue)Handle(key.Value);else Thread.Sleep(12);}
    }
    void HandlePopup(KeyEvent k)
    {
        var p=popup!;
        if(k.Key=="esc"){popup=null;return;}
        if(p.Content!=null)
        {
            switch(k.Key){case "up":p.Scroll=Math.Max(0,p.Scroll-1);break;case "down":p.Scroll++;break;case "pageup":p.Scroll=Math.Max(0,p.Scroll-height+8);break;case "pagedown":p.Scroll+=Math.Max(1,height-8);break;case "home":p.Scroll=0;break;case "end":p.Scroll=int.MaxValue;break;case "enter":popup=null;break;case "backspace":if(p.Text.Length>0)p.Text=p.Text[..^1];p.Scroll=0;break;default:if(k.Text.Length>0){p.Text+=k.Text;p.Scroll=0;}break;}return;
        }
        var items=p.Prompt?p.Items:p.Filtered;
        if(k.Key is "up" or "down" or "pageup" or "pagedown") {p.Index=Math.Clamp(p.Index+(k.Key=="up"?-1:k.Key=="down"?1:k.Key=="pageup"?-10:10),0,Math.Max(0,items.Count-1));return;}
        if(k.Key=="tab"&&p.Prompt&&items.Count>0){p.Text=items[Math.Clamp(p.Index,0,items.Count-1)].Label;p.Index=(p.Index+1)%items.Count;p.Changed?.Invoke(p.Text);return;}
        if(k.Key=="ctrl+a"&&p.Prompt){p.Text="";p.Changed?.Invoke(p.Text);return;}
        if(k.Key is "enter" or "ctrl+v" or "ctrl+h" or "ctrl+t")
        {
            string value=p.Prompt?p.Text:items.ElementAtOrDefault(p.Index)?.Value??"";
            if(!p.Prompt&&items.Count==0)return;
            popup=null;p.Accept?.Invoke(value,k.Key);return;
        }
        if(k.Key=="backspace") {if(p.Text.Length>0)p.Text=p.Text[..^1];p.Index=0;p.Changed?.Invoke(p.Text);}
        else if(k.Text.Length>0||k.Paste){p.Text+=k.Text.Replace('\n',' ');p.Index=0;p.Changed?.Invoke(p.Text);}
    }
    void Draw()
    {
        try{width=Math.Clamp(terminal.Width,20,500);height=Math.Clamp(terminal.Height,8,200);}catch(IOException ex){ErrorLog.Write(ex,"Read terminal dimensions");width=100;height=30;}
        var s=new Screen(width,height,foreground,background);int cx=-1,cy=-1;
        menuHits.Clear();tabHits.Clear();paneHits.Clear();popupHits.Clear();
        s.Fill(new(0,0,width,1),"#283950");s.Put(1,0," RUDYEDIT  ","#e4f2ff","#283950",1,width-2);
        int menuX=12;
        foreach(string name in MainMenus)
        {
            var icon=UiIcons.Menu(name);string prefix=settings.UseNerdIcons?icon.Glyph+" ":"";
            int prefixW=Screen.DisplayWidth(prefix),visible=Math.Min(prefixW+name.Length,width-menuX);
            if(visible<=0)break;
            menuHits.Add((new(menuX,0,visible,1),name));
            if(prefixW>0)s.Put(menuX,0,prefix,icon.Color,"#283950",0,visible);
            int nameX=menuX+prefixW,nameW=visible-prefixW;
            if(nameW>0){s.Put(nameX,0,name[..1],"#e4f2ff","#283950",5,nameW);s.Put(nameX+1,0,name[1..],"#e4f2ff","#283950",1,nameW-1);}
            menuX+=prefixW+name.Length+3;
        }
        int bars=mode=="Text"&&showTagBar?2:0,top=mode=="Text"?2:1,bottom=height-2-bars;
        int consoleH=showConsole?Math.Min(Math.Clamp(height*settings.ConsoleHeight/100,5,Math.Max(5,height/2)),Math.Max(0,bottom-top-3)):0;
        int fileW=showFiles?Math.Clamp(width*settings.FileManagerWidth/100,18,Math.Max(18,width/2)):0;
        var editorArea=new Rect(fileW,top,width-fileW,Math.Max(0,bottom-top-consoleH));
        fileBounds=new(0,top+1,Math.Max(0,fileW-1),Math.Max(0,bottom-top-1-consoleH-(mode=="Text"?1:0)));
        consoleBounds=new(0,bottom-consoleH,width,consoleH);
        if(showFiles)
        {
            s.Fill(new(0,top,fileW,Math.Max(0,bottom-top-consoleH)),"#1b2636");s.Put(1,top,(filesFocus?"▸ ":"  ")+(settings.UseNerdIcons?UiIcons.Entry(browser.Folder,true,opened:true).Glyph+" ":"")+Path.GetFileName(browser.Folder),"#80c9ff","#1b2636",1,fileW-2);
            int visible=fileBounds.H;if(browser.Index<browser.Scroll)browser.Scroll=browser.Index;if(browser.Index>=browser.Scroll+visible)browser.Scroll=browser.Index-visible+1;
            for(int i=0;i<visible;i++)
            {
                int index=browser.Scroll+i;if(index<0||index>=browser.Entries.Count)break;
                string path=browser.Entries[index];bool directory=Directory.Exists(path),parent=browser.TagFilter.Length==0&&index==0&&Directory.GetParent(browser.Folder)?.FullName==path;
                var icon=UiIcons.Entry(path,directory,parent);string symbol=settings.UseNerdIcons?icon.Glyph:icon.Plain;
                string label=parent?"..":browser.TagFilter.Length>0?Path.GetRelativePath(browser.Folder,path):Path.GetFileName(path);
                string bg=index==browser.Index?"#334c68":"#1b2636";s.Fill(new(0,i+top+1,fileW-1,1),bg);
                int iconW=Screen.DisplayWidth(symbol);s.Put(1,i+top+1,symbol,icon.Color,bg,0,fileW-2);
                s.Put(2+iconW,i+top+1,label,directory?"#e6bc78":"#c2cfe0",bg,0,Math.Max(0,fileW-3-iconW));
            }
            for(int y=top;y<bottom-consoleH;y++)s.Put(fileW-1,y,"│","#425773",background);
        }
        foreach(var (pane,r) in layout.Arrange(editorArea))
        {
            if(r.W<6||r.H<3)continue;bool focused=pane==active&&!filesFocus&&!consoleFocus&&barFocus.Length==0;
            string tabBg=focused?"#334c68":"#202b3c";s.Fill(new(r.X,r.Y,r.W,1),tabBg);int tx=r.X+1;
            for(int t=0;t<pane.Tabs.Count;t++){var d=pane.Tabs[t];string label=$" {(t==pane.Selected?"▸":"")} {d.Name}{(d.Dirty?" *":"")} ";int labelWidth=Math.Min(Screen.DisplayWidth(label),Math.Max(0,r.X+r.W-tx));tabHits.Add((new(tx,r.Y,labelWidth,1),pane,t));s.Put(tx,r.Y,label,t==pane.Selected?"#ffffff":"#8c9fb7",tabBg,t==pane.Selected?1:0,labelWidth);tx+=Screen.DisplayWidth(label);if(tx>=r.X+r.W)break;}
            var doc=pane.Document;var lines=doc.Lines;var pos=doc.Position;int bodyH=r.H-1,gutter=settings.LineNumbers?Math.Max(4,lines.Length.ToString().Length+3):2,bodyW=Math.Max(1,r.W-gutter-1);
            paneHits.Add((new(r.X,r.Y+1,r.W,bodyH),pane,gutter));
            if(pane.FollowCursor){if(pos.Row<pane.ScrollY)pane.ScrollY=pos.Row;if(pos.Row>=pane.ScrollY+bodyH)pane.ScrollY=pos.Row-bodyH+1;}
            int cursorCell=Screen.DisplayWidth(lines[pos.Row][..pos.Column],settings.DefaultTab);
            if(pane.FollowCursor){if(cursorCell<pane.ScrollX)pane.ScrollX=cursorCell;if(cursorCell>=pane.ScrollX+bodyW)pane.ScrollX=cursorCell-bodyW+1;}
            int offset=lines.Take(pane.ScrollY).Sum(l=>l.Length+1);var selection=doc.Selection;
            for(int row=0;row<bodyH;row++)
            {
                int lineNo=row+pane.ScrollY,y=r.Y+1+row;if(lineNo>=lines.Length)break;
                string text=lines[lineNo];bool isDebug=doc.FilePath==debugFile&&debugLine==lineNo+1;bool bp=doc.FilePath!=null&&breakpoints.TryGetValue(doc.FilePath,out var bps)&&bps.Contains(lineNo+1);
                string rowBg=isDebug?"#403b25":background;s.Fill(new(r.X,y,r.W-1,1),rowBg);
                s.Put(r.X,y,isDebug?"▶":bp?"●":" ",bp?"#ff7777":"#ffcc66",rowBg);
                if(settings.LineNumbers)s.Put(r.X+1,y,(lineNo+1).ToString().PadLeft(gutter-2)+" ","#60738f",rowBg,0,gutter-1);
                var tokens=Languages.Highlight(text,doc.Language);var miss=settings.SpellCheck&&mode=="Text"?spell.Unknown(text).ToList():[];
                int x=0,index=0;
                foreach(var rune in text.EnumerateRunes())
                {
                    int count=rune.Utf16SequenceLength,w=rune.Value=='\t'?settings.DefaultTab-x%settings.DefaultTab:Screen.CharWidth(rune);string glyph=rune.Value=='\t'?new string(' ',w):rune.ToString();
                    var token=tokens.ElementAtOrDefault(index);string fg=token.Color??foreground;if(fg=="#d8dee9")fg=foreground;int style=token.Style;
                    if(miss.Any(m=>index>=m.Index&&index<m.Index+m.Word.Length))style|=4;
                    string bg=offset+index>=selection.Start&&offset+index<selection.Start+selection.Length?"#435e80":rowBg;
                    if(x>=pane.ScrollX&&x-pane.ScrollX+w<=bodyW)s.Put(r.X+gutter+x-pane.ScrollX,y,glyph,fg,bg,style,bodyW-(x-pane.ScrollX));
                    x+=w;index+=count;if(x-pane.ScrollX>=bodyW)break;
                }
                offset+=text.Length+1;
            }
            if(r.X+r.W<width)for(int y=r.Y;y<r.Y+r.H;y++)s.Put(r.X+r.W-1,y,"│","#425773",background);
            if(focused&&pos.Row>=pane.ScrollY&&pos.Row<pane.ScrollY+bodyH&&cursorCell>=pane.ScrollX&&cursorCell<pane.ScrollX+bodyW){cx=r.X+gutter+cursorCell-pane.ScrollX;cy=r.Y+1+pos.Row-pane.ScrollY;}
        }
        if(showConsole)
        {
            int consoleTop=bottom-consoleH;s.Fill(new(0,consoleTop,width,consoleH),"#101722");s.Put(1,consoleTop," Console"+(runner.Active?" [running]":"")+(debugger?.Active==true?" [debug]":"")+" — Esc editor • Ctrl+` focus • Shift+F5 stop", "#80c9ff","#101722",1,width-2);
            var rows=output.SelectMany(x=>x.Replace('\r',' ').Split('\n')).ToList();int start=Math.Max(0,rows.Count-(consoleH-2)-consoleScroll);
            for(int y=0;y<consoleH-2&&start+y<rows.Count;y++)s.Put(1,consoleTop+1+y,rows[start+y],"#b8c8da","#101722",0,width-2);
            s.Put(1,bottom-1,(consoleFocus?"▸ ":"$ ")+commandLine,"#d9e9fa","#101722",0,width-2);if(consoleFocus){cx=Math.Min(width-2,3+Screen.DisplayWidth(commandLine));cy=bottom-1;}
        }
        DrawTextBars(s,fileW,bottom,bottom-consoleH-1,ref cx,ref cy);
        var position=Doc.Position;s.Fill(new(0,height-2,width,1),"#283950");s.Put(1,height-2,$"{mode} | {keysMode}{(keysMode=="VIM"?" "+vim:"")} | {Languages.Get(Doc.Language).Name} | {position.Row+1}:{position.Column+1} | {working}","#e0eaf5","#283950",0,width-2);
        s.Put(1,height-1,vimCommand?":"+commandLine:chord.Length>0?"Keys: "+chord:message,"#a9bfd8",background,0,width-2);
        if(vimCommand){cx=2+commandLine.Length;cy=height-1;}
        if(popup!=null){DrawPopup(s,popup);cx=cy=-1;}
        terminal.Draw(s,cx,cy);
    }
    static List<string> WrapText(string content,int width)
    {
        var result=new List<string>();foreach(string line in content.Replace('\t',' ').Split('\n'))
        {
            if(line.Length==0){result.Add("");continue;}
            var part=new StringBuilder();int w=0;foreach(var r in line.EnumerateRunes()){int n=Screen.CharWidth(r);if(w+n>width&&part.Length>0){result.Add(part.ToString());part.Clear();w=0;}part.Append(r);w+=n;}result.Add(part.ToString());
        }return result;
    }
    void DrawPopup(Screen s,Popup p)
    {
        int w=Math.Max(16,width-6),h=Math.Max(6,height-4);if(p.Dropdown){w=Math.Min(w,Math.Max(24,p.Items.Select(c=>Screen.DisplayWidth(c.Label)+4).DefaultIfEmpty(24).Max()));h=Math.Min(h,Math.Max(6,p.Items.Count+4));}
        var r=new Rect(p.Dropdown?1:(width-w)/2,p.Dropdown?2:(height-h)/2,w,h);s.Box(r,p.Title);
        popupBounds=r;popupClose=new(r.X+r.W-3,r.Y,1,1);s.Put(popupClose.X,popupClose.Y,"×","#eff7ff","#202c3e",1);
        s.Put(r.X+2,r.Y+1,(p.Prompt?"> ":"Search: ")+p.Text,"#e4f2ff","#202c3e",0,w-4);
        int visible=h-4;
        if(p.Content!=null)
        {
            var lines=p.Text.Length==0?p.Content.Split('\n').ToList():p.Content.Split('\n').Where(line=>line.Contains(p.Text,StringComparison.OrdinalIgnoreCase)).ToList();
            lines=WrapText(string.Join('\n',lines),w-4);p.Scroll=Math.Clamp(p.Scroll,0,Math.Max(0,lines.Count-visible));
            for(int i=0;i<visible&&i+p.Scroll<lines.Count;i++)
            {
                string line=lines[i+p.Scroll];int style=0;string fg="#cedbeb";
                if(ReferenceEquals(p,previewPopup)){if(line.StartsWith('#')){line=line.TrimStart('#',' ');style=1;fg="#ffcc88";}else if(line.StartsWith("- "))line="• "+line[2..];if(line.Contains("**")){style|=1;line=line.Replace("**","");}if(line.Contains("<u>")){style|=4;line=line.Replace("<u>","").Replace("</u>","");}var colour=Regex.Match(line,"color:(#[0-9a-fA-F]{6})");if(colour.Success)fg=colour.Groups[1].Value;line=WebUtility.HtmlDecode(Regex.Replace(line,"<[^>]+>",""));}
                s.Put(r.X+2,r.Y+2+i,line,fg,"#202c3e",style,w-4);
            }
            s.Put(r.X+2,r.Y+h-1,$" Esc close • ↑↓ / PgUp PgDn scroll • {p.Scroll+1}/{lines.Count} ","#9cb6d5","#202c3e",0,w-4);return;
        }
        var items=p.Prompt?p.Items:p.Filtered;p.Index=Math.Clamp(p.Index,0,Math.Max(0,items.Count-1));
        int detailH=!p.Prompt&&items.Count>0&&items[p.Index].Detail.Length>0?Math.Min(6,visible/2):0;int listH=Math.Max(1,visible-detailH);
        if(p.Index<p.Scroll)p.Scroll=p.Index;if(p.Index>=p.Scroll+listH)p.Scroll=p.Index-listH+1;
        for(int i=0;i<listH&&i+p.Scroll<items.Count;i++){int idx=i+p.Scroll;var hit=new Rect(r.X+1,r.Y+2+i,w-2,1);popupHits.Add((hit,idx));string bg=idx==p.Index?"#3b5678":"#202c3e";s.Fill(hit,bg);s.Put(r.X+2,r.Y+2+i,items[idx].Label,idx==p.Index?"#ffffff":"#bdcde1",bg,idx==p.Index?1:0,w-4);}
        if(items.Count==0)s.Put(r.X+2,r.Y+3,p.Prompt?"Type a value; Ctrl+A clears it.":"No matches.","#8dabc9","#202c3e",0,w-4);
        if(detailH>0){var details=WrapText(items[p.Index].Detail,w-4);for(int i=0;i<detailH&&i<details.Count;i++)s.Put(r.X+2,r.Y+h-2-detailH+i,details[i],"#a2bad8","#202c3e",0,w-4);}
        s.Put(r.X+2,r.Y+h-1," Enter select • Esc cancel • type to "+(p.Prompt?"edit • Ctrl+A clear • Tab complete":"filter"),"#9cb6d5","#202c3e",0,w-4);
    }
}
