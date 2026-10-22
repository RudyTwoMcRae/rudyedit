using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
namespace RudyEdit;
public enum MouseAction { Press, Move, Release, Wheel }
public readonly record struct MouseEvent(int X, int Y, MouseAction Action, int Button = 0, int Wheel = 0, bool Shift = false);
public readonly record struct KeyEvent(string Key, string Text = "", bool Paste = false, MouseEvent? Mouse = null);
public static class InputParser
{
    public static KeyEvent ConsoleKey(ConsoleKeyInfo k)
    {
        if (k.Key == 0 && k.KeyChar != 0 && k.Modifiers == 0) return Character(k.KeyChar);
        string key = k.Key switch
        {
            System.ConsoleKey.LeftArrow => "left", System.ConsoleKey.RightArrow => "right",
            System.ConsoleKey.UpArrow => "up", System.ConsoleKey.DownArrow => "down",
            System.ConsoleKey.Spacebar => "space", System.ConsoleKey.Escape => "esc",
            System.ConsoleKey.Oem3 => "`",
            >= System.ConsoleKey.D0 and <= System.ConsoleKey.D9 => ((int)k.Key - (int)System.ConsoleKey.D0).ToString(),
            System.ConsoleKey.OemMinus => "-", System.ConsoleKey.OemPlus => "=",
            System.ConsoleKey.OemComma => ",", System.ConsoleKey.OemPeriod => ".",
            System.ConsoleKey.Oem1 => ";", System.ConsoleKey.Oem2 => "/",
            System.ConsoleKey.Oem4 => "[", System.ConsoleKey.Oem5 => "\\", System.ConsoleKey.Oem6 => "]", System.ConsoleKey.Oem7 => "'",
            0 => k.KeyChar is >= '\x01' and <= '\x1a' ? ((char)('a' + k.KeyChar - 1)).ToString() :
                k.KeyChar == 0 ? "space" : k.KeyChar.ToString().ToLowerInvariant(), _ => k.Key.ToString().ToLowerInvariant()
        };
        string mods = (k.Modifiers.HasFlag(ConsoleModifiers.Control) ? "ctrl+" : "") +
            (k.Modifiers.HasFlag(ConsoleModifiers.Alt) ? "alt+" : "") +
            (k.Modifiers.HasFlag(ConsoleModifiers.Shift) ? "shift+" : "");
        return new(mods + key, (k.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) == 0 && !char.IsControl(k.KeyChar) ? k.KeyChar.ToString() : "");
    }
    public static KeyEvent Character(char c) => c switch
    {
        '\r' or '\n' => new("enter"), '\t' => new("tab"), '\x7f' or '\b' => new("backspace"), '\0' => new("ctrl+space"),
        >= '\x01' and <= '\x1a' => new("ctrl+" + (char)('a' + c - 1)), '\x1b' => new("esc"),
        _ => new(char.IsUpper(c) ? "shift+" + char.ToLowerInvariant(c) : c == ' ' ? "space" : c.ToString(), c.ToString())
    };
    public static KeyEvent EscapeSequence(string seq)
    {
        if(seq.StartsWith("[<",StringComparison.Ordinal))
        {
            var mouse=Regex.Match(seq,@"^\[<(\d+);(\d+);(\d+)([Mm])$");
            if(!mouse.Success||!int.TryParse(mouse.Groups[1].Value,out int code)||
               !int.TryParse(mouse.Groups[2].Value,out int x)||!int.TryParse(mouse.Groups[3].Value,out int y)||
               x<1||y<1||code>=128)return new("unknown");
            int button=code&3;bool shift=(code&4)!=0;
            if((code&64)!=0)return button>1?new("unknown"):new("mouse",Mouse:new(x-1,y-1,MouseAction.Wheel,Wheel:button==0?-1:1,Shift:shift));
            var action=mouse.Groups[4].Value=="m"?MouseAction.Release:(code&32)!=0?MouseAction.Move:MouseAction.Press;
            return new("mouse",Mouse:new(x-1,y-1,action,button,Shift:shift));
        }
        var basic = new Dictionary<string,string> { ["[A"]="up",["[B"]="down",["[C"]="right",["[D"]="left",["[H"]="home",["[F"]="end",["[Z"]="shift+tab",["OP"]="f1",["OQ"]="f2",["OR"]="f3",["OS"]="f4",["[1~"]="home",["[4~"]="end",["[2~"]="insert",["[3~"]="delete",["[5~"]="pageup",["[6~"]="pagedown",["[7~"]="home",["[8~"]="end",["[11~"]="f1",["[12~"]="f2",["[13~"]="f3",["[14~"]="f4",["[15~"]="f5",["[17~"]="f6",["[18~"]="f7",["[19~"]="f8",["[20~"]="f9",["[21~"]="f10",["[23~"]="f11",["[24~"]="f12" };
        if (basic.TryGetValue(seq, out var name)) return new(name);
        // Kitty/Ghostty may omit both the default key parameter and modifiers.
        // Accept CSI P and CSI 1P as well as SS3 P and CSI 1;1P (and F2-F4).
        var function = Regex.Match(seq, "^\\[(?:1)?([PQRS])$");
        if (function.Success) return new("f" + (function.Groups[1].Value[0] - 'P' + 1));
        var m = Regex.Match(seq, "^\\[(\\d+);(\\d+)([A-Za-z~])$");
        if (m.Success)
        {
            if (!int.TryParse(m.Groups[2].Value, out int encodedMods) || encodedMods < 1) return new("unknown");
            int mods = encodedMods - 1;
            string? key = m.Groups[3].Value == "~" ? basic.GetValueOrDefault("[" + m.Groups[1].Value + "~") : basic.GetValueOrDefault("[" + m.Groups[3].Value);
            if (m.Groups[3].Value is "P" or "Q" or "R" or "S") key = "f" + (m.Groups[3].Value[0] - 'P' + 1);
            if (key != null) return new(Modifiers(mods) + key);
        }
        m = Regex.Match(seq, @"^\[(\d+)(?::(\d*)(?::\d+)?)?(?:;(\d*)(?::([123]))?)?(?:;([\d:]+))?u$");
        if (m.Success)
        {
            if (!int.TryParse(m.Groups[1].Value, out int code) || !Rune.IsValid(code)) return new("unknown");
            int encodedMods = 1;
            if (m.Groups[3].Value.Length > 0 && (!int.TryParse(m.Groups[3].Value, out encodedMods) || encodedMods < 1)) return new("unknown");
            if (m.Groups[4].Value == "3") return new("unknown"); // Key-up is not another command.
            int mods = encodedMods - 1;
            string? name2 = code switch
            {
                32 => "space", 13 or 57345 => "enter", 9 or 57346 => "tab", 27 or 57344 => "esc", 127 or 57347 => "backspace",
                57348 => "insert", 57349 => "delete", 57350 => "left", 57351 => "right", 57352 => "up", 57353 => "down",
                57354 => "pageup", 57355 => "pagedown", 57356 => "home", 57357 => "end",
                >= 57364 and <= 57398 => "f" + (code - 57363),
                >= 57344 and <= 63743 => null,
                _ => char.ConvertFromUtf32(code).ToLowerInvariant()
            };
            if (name2 == null) return new("unknown");
            string text = "";
            if ((mods & 62) == 0) // Ctrl/Alt/Super/Hyper/Meta must never become text.
            {
                if (m.Groups[5].Success)
                {
                    var value = new StringBuilder();
                    foreach (string part in m.Groups[5].Value.Split(':'))
                    {
                        if (!int.TryParse(part, out int cp) || !Rune.IsValid(cp) || Rune.IsControl(new Rune(cp))) return new("unknown");
                        value.Append(char.ConvertFromUtf32(cp));
                    }
                    text = value.ToString();
                }
                else if (code >= 32 && code < 57344 || code > 63743)
                {
                    if (!Rune.IsControl(new Rune(code))) text = char.ConvertFromUtf32(code);
                    if ((mods & 1) != 0)
                    {
                        if (int.TryParse(m.Groups[2].Value, out int shifted) && Rune.IsValid(shifted) && !Rune.IsControl(new Rune(shifted))) text = char.ConvertFromUtf32(shifted);
                        else text = text.ToUpperInvariant();
                    }
                }
            }
            return new(Modifiers(mods) + name2, text);
        }
        // xterm modifyOtherKeys, used by some Windows terminal frontends.
        m = Regex.Match(seq, @"^\[27;(\d+);(\d+)~$");
        if (m.Success) return EscapeSequence("[" + m.Groups[2].Value + ";" + m.Groups[1].Value + "u");
        if (seq.Length == 1) return new("alt+" + seq.ToLowerInvariant());
        return new("unknown");
    }
    static string Modifiers(int mods) => ((mods & 4) != 0 ? "ctrl+" : "") + ((mods & 2) != 0 ? "alt+" : "") + ((mods & 1) != 0 ? "shift+" : "");
}
public interface ITerminal : IDisposable
{
    KeyEvent? ReadKey();
    void Draw(Screen screen, int cursorX = -1, int cursorY = -1);
    void Invalidate();
    int Width { get; }
    int Height { get; }
}
public sealed class Terminal : ITerminal
{
    string? oldStty;
    uint? oldInputMode, oldOutputMode;
    readonly Encoding oldOutputEncoding = Console.OutputEncoding;
    bool disposed;
    readonly WindowsConsoleInput windowsInput = new();
    readonly TerminalInputBuffer input = new();
    readonly Decoder decoder = Encoding.UTF8.GetDecoder();
    public int Width => Console.WindowWidth;
    public int Height => Console.WindowHeight;
    public static string StartupSequence(bool windows) => "\x1b[?1049h\x1b[?25l" +
        (windows ? "\x1b[>0u\x1b[?2004l" : "\x1b[?2004h\x1b[>1u\x1b[?1002h\x1b[?1006h");
    public const string ShutdownSequence = "\x1b[?1002l\x1b[?1006l\x1b[0m\x1b[<u\x1b[?2004l\x1b[?25h\x1b[?1049l";
    public static uint WindowsInputMode(uint saved) => (saved | 0x0098u) & ~0x0247u;
    string[] previous = [];
    int previousCursorX=-2,previousCursorY=-2;
    [StructLayout(LayoutKind.Sequential)] struct PollFd { public int fd; public short events; public short revents; }
    [DllImport("libc", SetLastError=true)] static extern int poll(ref PollFd fds, uint count, int timeout);
    [DllImport("libc", SetLastError=true)] static extern nint read(int fd, byte[] buffer, nuint count);
    [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int n);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetConsoleMode(IntPtr handle, out uint mode);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetConsoleMode(IntPtr handle, uint mode);
    public Terminal()
    {
        try
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            if (OperatingSystem.IsWindows())
            {
                var hIn = GetStdHandle(-10); var hOut = GetStdHandle(-11);
                CheckMode(GetConsoleMode(hIn, out uint inputMode), "read input mode"); oldInputMode = inputMode;
                CheckMode(GetConsoleMode(hOut, out uint outputMode), "read output mode"); oldOutputMode = outputMode;
                CheckMode(SetConsoleMode(hIn, WindowsInputMode(inputMode)), "enable native keyboard and mouse input");
                CheckMode(SetConsoleMode(hOut, outputMode | 5u), "enable terminal drawing");
            }
            else { oldStty = Stty("-g").Trim(); Stty("raw -echo"); }
            Console.Write(StartupSequence(OperatingSystem.IsWindows()));
        }
        catch(Exception ex) { ErrorLog.Write(ex,"Initialize terminal");Dispose();throw; }
    }
    static void CheckMode(bool success, string operation)
    {
        if (!success) throw new IOException("Could not " + operation + ": " + new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message);
    }
    static string Stty(string args)
    {
        using var p = Process.Start(new ProcessStartInfo("stty", args) { RedirectStandardOutput = true, RedirectStandardError = true })!;
        var result = p.StandardOutput.ReadToEnd(); p.WaitForExit(); if (p.ExitCode != 0) throw new IOException("Could not configure terminal: " + p.StandardError.ReadToEnd()); return result;
    }
    public KeyEvent? ReadKey()
    {
        if (OperatingSystem.IsWindows())
        {
            return windowsInput.Read();
        }
        var fd = new PollFd { fd = 0, events = 1 };
        if (poll(ref fd, 1, 0) > 0)
        {
            var bytes = new byte[8192]; int count = (int)read(0, bytes, (nuint)bytes.Length);
            if (count == 0) return new("ctrl+q");
            if (count > 0) { var chars = new char[8192]; int n = decoder.GetChars(bytes, 0, count, chars, 0); input.Append(new string(chars, 0, n)); }
        }
        return input.Read();
    }
    public void Draw(Screen screen, int cursorX = -1, int cursorY = -1)
    {
        var rows = screen.Rows();
        if (rows.SequenceEqual(previous) && cursorX==previousCursorX && cursorY==previousCursorY) return;
        var output = new StringBuilder("\x1b[?25l");
        for (int y = 0; y < rows.Length; y++) if (y >= previous.Length || previous[y] != rows[y]) output.Append($"\x1b[{y + 1};1H").Append(rows[y]);
        if (cursorX >= 0 && cursorY >= 0 && cursorX < screen.Width && cursorY < screen.Height) output.Append($"\x1b[{cursorY + 1};{cursorX + 1}H\x1b[?25h");
        Console.Write(output); previous = rows; previousCursorX=cursorX; previousCursorY=cursorY;
    }
    public void Invalidate() => previous = [];
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { Console.Write(ShutdownSequence); }
        finally
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    if (oldInputMode is uint inputMode) SetConsoleMode(GetStdHandle(-10), inputMode);
                    if (oldOutputMode is uint outputMode) SetConsoleMode(GetStdHandle(-11), outputMode);
                }
                else if (oldStty != null) Stty(oldStty);
            }
            finally { Console.OutputEncoding = oldOutputEncoding; }
        }
    }
}
public readonly record struct Rect(int X, int Y, int W, int H);
public readonly record struct Cell(string Text, string Fg, string Bg, int Style = 0);
public sealed class Screen
{
    public int Width { get; } public int Height { get; }
    readonly Cell[,] cells;
    public Screen(int width, int height, string fg, string bg) { Width = width; Height = height; cells = new Cell[height,width]; for (int y=0;y<height;y++) for(int x=0;x<width;x++) cells[y,x] = new(" ",fg,bg); }
    public static int CharWidth(Rune r)
    {
        var category = Rune.GetUnicodeCategory(r);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format) return 0;
        int c = r.Value; return c >= 0x1100 && (c <= 0x115f || c is >= 0x2e80 and <= 0xa4cf || c is >= 0xac00 and <= 0xd7a3 || c is >= 0xf900 and <= 0xfaff || c is >= 0xfe10 and <= 0xfe6f || c is >= 0xff01 and <= 0xff60 || c is >= 0x1f300 and <= 0x1faff || c >= 0x20000) ? 2 : 1;
    }
    public static int DisplayWidth(string s, int tab=4) { int w=0; foreach(var r in s.EnumerateRunes()) w += r.Value == '\t' ? tab - w % tab : Rune.IsControl(r) ? 1 : CharWidth(r); return w; }
    public static int ColumnAtCell(string text,int cell,int tab=4)
    {
        int width=0;
        var elements=StringInfo.GetTextElementEnumerator(text);
        while(elements.MoveNext())
        {
            string element=elements.GetTextElement();
            int n=element=="\t"?tab-width%tab:DisplayWidth(element,tab);
            if(cell<width+n)return elements.ElementIndex;
            width+=n;
        }
        return text.Length;
    }
    public void Put(int x, int y, string text, string fg = "#d8dee9", string bg = "#151c28", int style = 0, int limit = int.MaxValue)
    {
        if (y < 0 || y >= Height) return; int end = Math.Min(Width, x + Math.Min(limit, Width));
        foreach (var r in text.EnumerateRunes())
        {
            int w = Rune.IsControl(r) ? 1 : CharWidth(r); string glyph = Rune.IsControl(r) ? "?" : r.ToString();
            if (w == 0) { if (x>0 && x<=Width) cells[y,x-1] = cells[y,x-1] with { Text = cells[y,x-1].Text + glyph }; continue; }
            if (x+w>end) break;
            if (x>=0) { cells[y,x] = new(glyph,fg,bg,style); if(w==2) cells[y,x+1]=new("",fg,bg,style); } x+=w;
        }
    }
    public void Fill(Rect r, string bg, string fg = "#d8dee9") { for(int y=Math.Max(0,r.Y);y<Math.Min(Height,r.Y+r.H);y++) for(int x=Math.Max(0,r.X);x<Math.Min(Width,r.X+r.W);x++) cells[y,x]=new(" ",fg,bg); }
    public void Box(Rect r, string title)
    {
        Fill(r,"#202c3e"); Put(r.X,r.Y,"┌"+new string('─',Math.Max(0,r.W-2))+"┐","#77c8dc","#202c3e");
        for(int y=r.Y+1;y<r.Y+r.H-1;y++) { Put(r.X,y,"│","#77c8dc","#202c3e");Put(r.X+r.W-1,y,"│","#77c8dc","#202c3e"); }
        Put(r.X,r.Y+r.H-1,"└"+new string('─',Math.Max(0,r.W-2))+"┘","#77c8dc","#202c3e"); Put(r.X+2,r.Y," "+title+" ","#eff7ff","#202c3e",1,r.W-4);
    }
    public string[] Rows()
    {
        var rows = new string[Height];
        for(int y=0;y<Height;y++) { var s = new StringBuilder(); Cell? prev=null; for(int x=0;x<Width;x++) { var c=cells[y,x]; if(prev==null||prev.Value.Fg!=c.Fg||prev.Value.Bg!=c.Bg||prev.Value.Style!=c.Style) { s.Append("\x1b[0;").Append((c.Style&1)!=0?"1;":"").Append((c.Style&2)!=0?"3;":"").Append((c.Style&4)!=0?"4;":"").Append(Rgb(c.Fg,38)).Append(';').Append(Rgb(c.Bg,48)).Append('m'); } s.Append(c.Text); prev=c; } rows[y]=s.Append("\x1b[0m").ToString(); } return rows;
    }
    static string Rgb(string hex, int mode) => $"{mode};2;{Convert.ToInt32(hex.Substring(1,2),16)};{Convert.ToInt32(hex.Substring(3,2),16)};{Convert.ToInt32(hex.Substring(5,2),16)}";
}
public sealed class Pane
{
    public bool FollowCursor { get; set; } = true;
    public List<Document> Tabs { get; } = [new()]; public int Selected { get; set; } public int ScrollY { get; set; } public int ScrollX { get; set; }
    public Document Document => Tabs[Math.Clamp(Selected,0,Tabs.Count-1)];
}
public sealed class Layout
{
    public Pane? Pane { get; private set; } = new();
    public Layout? First { get; private set; } public Layout? Second { get; private set; } public bool Vertical { get; private set; }
    public IEnumerable<Pane> Panes => Pane != null ? [Pane] : First!.Panes.Concat(Second!.Panes);
    public Dictionary<Pane,Rect> Arrange(Rect r)
    {
        if(Pane!=null) return new(){{Pane,r}};
        int half=Vertical?r.W/2:r.H/2;
        var a=Vertical?new Rect(r.X,r.Y,half,r.H):new Rect(r.X,r.Y,r.W,half);
        var b=Vertical?new Rect(r.X+half,r.Y,r.W-half,r.H):new Rect(r.X,r.Y+half,r.W,r.H-half);
        return First!.Arrange(a).Concat(Second!.Arrange(b)).ToDictionary(x=>x.Key,x=>x.Value);
    }
    public Pane Split(Pane pane, bool vertical)
    {
        if(Pane==pane) { Vertical=vertical; First=new(){Pane=Pane}; Second=new(); Pane=null; return Second.Pane!; }
        return First!.Panes.Contains(pane)?First.Split(pane,vertical):Second!.Split(pane,vertical);
    }
    public bool Remove(Pane pane)
    {
        if(Pane!=null) return false;
        if(First!.Pane==pane || Second!.Pane==pane) { var keep=First.Pane==pane?Second:First; Pane=keep!.Pane; First=keep.First; Second=keep.Second; Vertical=keep.Vertical; return true; }
        return First.Remove(pane)||Second!.Remove(pane);
    }
}
