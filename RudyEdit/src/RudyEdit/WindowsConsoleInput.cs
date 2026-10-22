using System.Runtime.InteropServices;
namespace RudyEdit;

// ReadConsoleInput preserves mouse records that Console.ReadKey discards.
public sealed class WindowsConsoleInput(Func<long>? clock = null)
{
    [StructLayout(LayoutKind.Explicit,Size=20,CharSet=CharSet.Unicode)]
    public struct Record
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(4)] public int KeyDown;
        [FieldOffset(8)] public ushort Repeat;
        [FieldOffset(10)] public ushort VirtualKey;
        [FieldOffset(12)] public ushort ScanCode;
        [FieldOffset(14)] public char Character;
        [FieldOffset(16)] public uint KeyControls;
        [FieldOffset(4)] public short X;
        [FieldOffset(6)] public short Y;
        [FieldOffset(8)] public uint Buttons;
        [FieldOffset(12)] public uint MouseControls;
        [FieldOffset(16)] public uint MouseFlags;
    }
    [StructLayout(LayoutKind.Sequential)] struct Coord { public short X,Y; }
    [StructLayout(LayoutKind.Sequential)] struct SmallRect { public short Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct BufferInfo { public Coord Size,Cursor;public ushort Attributes;public SmallRect Window;public Coord Maximum; }
    [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int n);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetNumberOfConsoleInputEvents(IntPtr handle,out uint count);
    [DllImport("kernel32.dll",EntryPoint="ReadConsoleInputW",SetLastError=true)] static extern bool ReadConsoleInput(IntPtr handle,out Record record,uint length,out uint read);
    [DllImport("kernel32.dll")] static extern bool GetConsoleScreenBufferInfo(IntPtr handle,out BufferInfo info);
    uint previousButtons;
    readonly TerminalInputBuffer stream = new(clock);
    readonly Queue<KeyEvent> ready = new();
    // Tests and ReadConsoleInputW share the complete input path.
    public void Feed(Record record, int windowLeft = 0, int windowTop = 0)
    {
        if (record.Type == 1)
        {
            if (record.KeyDown == 0 || IsModifier(record.VirtualKey)) return;
            int count = Math.Max(1, (int)record.Repeat);
            bool characterRecord = record.VirtualKey is 0 or 231;
            bool shortcut = (record.KeyControls & 15) != 0;
            if (!shortcut && (record.Character == '\x1b' || (record.Character != 0 && (characterRecord || stream.HasPending || char.IsSurrogate(record.Character)))))
            {
                for (int i = 0; i < count; i++) stream.Append(record.Character.ToString());
                DrainStream(); return;
            }
            DrainStream(flush: true);
            if (Decode(record, windowLeft, windowTop) is {} key)
                for (int i = 0; i < count; i++) ready.Enqueue(key);
        }
        else
        {
            // Mouse/resize events must not split a keyboard sequence.
            if (Decode(record, windowLeft, windowTop) is {} key) ready.Enqueue(key);
        }
    }
    static bool IsModifier(ushort key) => key is 16 or 17 or 18 or 20 or 91 or 92 or 144 or 145 or >= 160 and <= 165;
    void DrainStream(bool flush = false)
    {
        while (stream.Read(flush) is {} key) ready.Enqueue(key);
    }
    public KeyEvent? TryRead()
    {
        DrainStream();
        return ready.TryDequeue(out var key) ? key : null;
    }
    public KeyEvent? Read()
    {
        if (TryRead() is {} queued) return queued;
        var handle=GetStdHandle(-10);
        if(!GetNumberOfConsoleInputEvents(handle,out uint count))throw new IOException("Cannot read console input events.");
        while(count-->0)
        {
            if(!ReadConsoleInput(handle,out var record,1,out uint read)||read!=1)throw new IOException("Cannot read console input.");
            int left=0,top=0;
            if(record.Type==2&&GetConsoleScreenBufferInfo(GetStdHandle(-11),out var info)){left=info.Window.Left;top=info.Window.Top;}
            Feed(record,left,top);
            if (TryRead() is {} result) return result;
        }
        return TryRead();
    }
    public KeyEvent? Decode(Record record,int windowLeft=0,int windowTop=0)
    {
        if(record.Type==1)
        {
            if(record.KeyDown==0||IsModifier(record.VirtualKey)||record.VirtualKey==0&&record.Character==0)return null;
            // AltGr is reported as RightAlt + LeftCtrl. Preserve composed text.
            if ((record.KeyControls & 0x1f) is 9 or 25 && record.Character != 0 && !char.IsControl(record.Character))
                return InputParser.Character(record.Character);
            if(record.VirtualKey > 255) return new("unknown");
            return InputParser.ConsoleKey(new(record.Character,(ConsoleKey)record.VirtualKey,
                (record.KeyControls&16)!=0,(record.KeyControls&3)!=0,(record.KeyControls&12)!=0));
        }
        if(record.Type!=2)return null;
        int x=record.X-windowLeft,y=record.Y-windowTop;
        bool shift=(record.MouseControls&16)!=0;
        uint buttons=record.Buttons&0xffff;
        MouseEvent mouse;
        if((record.MouseFlags&4)!=0)
        {
            short delta=unchecked((short)(record.Buttons>>16));
            if(delta==0)return null;
            mouse=new(x,y,MouseAction.Wheel,Wheel:delta>0?-1:1,Shift:shift);
        }
        else
        {
            if((record.MouseFlags&8)!=0)return null;
            uint changed=previousButtons^buttons;
            uint which=changed!=0?changed:buttons;
            int button=(which&1)!=0?0:(which&4)!=0?1:(which&2)!=0?2:3;
            var action=(record.MouseFlags&1)!=0?MouseAction.Move:
                (buttons&which)!=0?MouseAction.Press:MouseAction.Release;
            mouse=new(x,y,action,button,Shift:shift);
        }
        previousButtons=buttons;
        return new("mouse",Mouse:mouse);
    }
}
