namespace RudyEdit;
public sealed partial class EditorApp
{
    readonly List<(Rect Bounds,string Menu)> menuHits=[];
    readonly List<(Rect Bounds,Pane Pane,int Tab)> tabHits=[];
    readonly List<(Rect Bounds,Pane Pane,int Gutter)> paneHits=[];
    readonly List<(Rect Bounds,int Index)> popupHits=[];
    Rect popupBounds,popupClose,fileBounds,consoleBounds;
    Pane? draggingPane;
    Document? draggingDocument;
    int dragAnchor;
    static bool Contains(Rect r,int x,int y)=>x>=r.X&&x<r.X+r.W&&y>=r.Y&&y<r.Y+r.H;
    void HandleMouse(MouseEvent mouse)
    {
        bool press=mouse.Action==MouseAction.Press&&mouse.Button==0;
        if(popup==null&&draggingPane!=null&&mouse.Action is MouseAction.Move or MouseAction.Release)
        {
            var hit=paneHits.FirstOrDefault(h=>h.Pane==draggingPane);
            if(hit.Pane!=null&&hit.Pane.Document==draggingDocument)
            {
                active=hit.Pane;
                if(mouse.Y<hit.Bounds.Y)active.ScrollY=Math.Max(0,active.ScrollY-1);
                if(mouse.Y>=hit.Bounds.Y+hit.Bounds.H)active.ScrollY=Math.Min(Doc.Lines.Length-1,active.ScrollY+1);
                PlaceCursor(hit,mouse.X,mouse.Y);
                Doc.Anchor=Doc.Cursor==dragAnchor?null:dragAnchor;
                if(keysMode=="VIM"&&vim!="INSERT")vim=Doc.Anchor.HasValue?"VISUAL":"NORMAL";
            }
            if(mouse.Action==MouseAction.Release)draggingPane=null;
            return;
        }
        if(mouse.Action==MouseAction.Release){draggingPane=null;return;}
        if(press)
        {
            draggingPane=null;
            foreach(var hit in menuHits)if(Contains(hit.Bounds,mouse.X,mouse.Y))
            {if(!quitting)throw new GlobalShortcut(hit.Menu);return;}
        }
        if(popup is {} p)
        {
            draggingPane=null;
            if(mouse.Action==MouseAction.Wheel)
            {
                if(p.Content!=null)p.Scroll=Math.Max(0,p.Scroll+mouse.Wheel*3);
                else p.Index=Math.Clamp(p.Index+mouse.Wheel*3,0,Math.Max(0,(p.Prompt?p.Items:p.Filtered).Count-1));
            }
            if(!press)return;
            if(Contains(popupClose,mouse.X,mouse.Y)){popup=null;return;}
            foreach(var hit in popupHits)if(Contains(hit.Bounds,mouse.X,mouse.Y))
            {
                p.Index=hit.Index;
                if(p.Prompt){p.Text=p.Items[hit.Index].Label;p.Changed?.Invoke(p.Text);}
                else HandlePopup(new("enter"));
                return;
            }
            if(!Contains(popupBounds,mouse.X,mouse.Y)&&!quitting)popup=null;
            return;
        }
        if(TextBarMouse(mouse))return;
        if(press)foreach(var hit in tabHits)if(Contains(hit.Bounds,mouse.X,mouse.Y))
        {
            FocusMousePane(hit.Pane);active.Selected=hit.Tab;active.ScrollX=active.ScrollY=0;return;
        }
        foreach(var hit in paneHits)if(Contains(hit.Bounds,mouse.X,mouse.Y))
        {
            if(mouse.Action==MouseAction.Wheel)
            {
                hit.Pane.FollowCursor=false;
                hit.Pane.ScrollY=Math.Clamp(hit.Pane.ScrollY+mouse.Wheel*3,0,Math.Max(0,hit.Pane.Document.Lines.Length-hit.Bounds.H));
            }
            else if(press)
            {
                FocusMousePane(hit.Pane);
                int anchor=mouse.Shift?(Doc.Anchor??Doc.Cursor):-1;
                PlaceCursor(hit,mouse.X,mouse.Y);
                dragAnchor=anchor<0?Doc.Cursor:anchor;
                Doc.Anchor=Doc.Cursor==dragAnchor?null:dragAnchor;
                draggingPane=active;draggingDocument=Doc;
                if(keysMode=="VIM"&&vim!="INSERT")vim=Doc.Anchor.HasValue?"VISUAL":"NORMAL";
            }
            return;
        }
        if(showFiles&&Contains(fileBounds,mouse.X,mouse.Y))
        {
            if(mouse.Action==MouseAction.Wheel)browser.Index=Math.Clamp(browser.Index+mouse.Wheel*3,0,Math.Max(0,browser.Entries.Count-1));
            else if(press){browser.Index=Math.Clamp(browser.Scroll+mouse.Y-fileBounds.Y,0,Math.Max(0,browser.Entries.Count-1));filesFocus=true;consoleFocus=false;}
        }
        if(showConsole&&Contains(consoleBounds,mouse.X,mouse.Y))
        {
            if(mouse.Action==MouseAction.Wheel)consoleScroll=Math.Max(0,consoleScroll-mouse.Wheel*3);
            else if(press){consoleFocus=true;filesFocus=false;}
        }
    }
    void FocusMousePane(Pane pane)
    {
        active=pane;active.FollowCursor=true;filesFocus=consoleFocus=vimCommand=false;
        chord=vimPending=commandLine="";lastServerText="";
    }
    void PlaceCursor((Rect Bounds,Pane Pane,int Gutter) hit,int x,int y)
    {
        var d=hit.Pane.Document;
        int row=Math.Clamp(hit.Pane.ScrollY+Math.Clamp(y-hit.Bounds.Y,0,hit.Bounds.H-1),0,d.Lines.Length-1);
        int cell=hit.Pane.ScrollX+Math.Max(0,x-hit.Bounds.X-hit.Gutter);
        d.SetPosition(row,Screen.ColumnAtCell(d.Lines[row],cell,settings.DefaultTab));
        hit.Pane.FollowCursor=true;
    }
}
