using System.Runtime.InteropServices;
using RudyEdit;
using Record = RudyEdit.WindowsConsoleInput.Record;

// Run in a dedicated Windows console with:
// dotnet run --project tests/RudyEdit.Tests -c Release -- --windows-input
// This exercises the OS calls; the cross-platform suite does not claim to do so.
static class WindowsNativeTests
{
    [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int n);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetConsoleMode(IntPtr h,out uint mode);
    [DllImport("kernel32.dll",EntryPoint="WriteConsoleInputW",CharSet=CharSet.Unicode,ExactSpelling=true,SetLastError=true)]
    static extern bool WriteConsoleInput(IntPtr h,[In] Record[] records,uint length,out uint written);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool FlushConsoleInputBuffer(IntPtr h);
    public static int Run(string? reportPath=null)
    {
        if(!OperatingSystem.IsWindows()){Console.Error.WriteLine("This test requires an interactive Windows console.");return 2;}
        var input=GetStdHandle(-10);var output=GetStdHandle(-11);
        if(!GetConsoleMode(input,out uint beforeIn)||!GetConsoleMode(output,out uint beforeOut))
        {Console.Error.WriteLine("Run in a dedicated interactive Windows Terminal or Command Prompt window.");return 2;}
        var encoding=Console.OutputEncoding;int checks=0;var results=new List<object>();
        void Check(bool ok,string name){results.Add(new{name,status=ok?"PASS":"FAIL",error=ok?"":"Native Windows console assertion failed"});if(!ok)throw new Exception(name);checks++;}
        void Inject(params Record[] records)=>Check(WriteConsoleInput(input,records,(uint)records.Length,out uint written)&&written==records.Length,"WriteConsoleInputW");
        KeyEvent Read(Terminal t)
        {
            var deadline=Environment.TickCount64+2000;
            while(Environment.TickCount64<deadline){if(t.ReadKey() is {} key)return key;Thread.Sleep(1);}
            throw new Exception("No input received within 2 seconds");
        }
        try
        {
            Check(FlushConsoleInputBuffer(input),"Flush private test console input");
            using(var terminal=new Terminal())
            {
                Check(GetConsoleMode(input,out uint mode)&&mode==Terminal.WindowsInputMode(beforeIn),"Native input mode");
                for(int f=1;f<=12;f++) for(int mods=0;mods<8;mods++)
                {
                    uint flags=((mods&1)!=0?16u:0)|((mods&2)!=0?2u:0)|((mods&4)!=0?8u:0);
                    Inject(new Record{Type=1,KeyDown=1,Repeat=1,VirtualKey=(ushort)(111+f),KeyControls=flags});
                    string expected=((mods&4)!=0?"ctrl+":"")+((mods&2)!=0?"alt+":"")+((mods&1)!=0?"shift+":"")+"f"+f;
                    Check(Read(terminal)==new KeyEvent(expected),expected);
                }
                for(char c='a';c<='z';c++)
                {
                    Inject(new Record{Type=1,KeyDown=1,Repeat=1,VirtualKey=(ushort)char.ToUpperInvariant(c),Character=(char)(c-'a'+1),KeyControls=8});
                    Check(Read(terminal)==new KeyEvent("ctrl+"+c),"Ctrl+"+c);
                }
                foreach(var (wire,key) in new[]{("\x1b[113;5u","ctrl+q"),("\x1bOP","f1"),("\x1b[1;2Q","shift+f2")})
                {
                    Inject(wire.Select(c=>new Record{Type=1,KeyDown=1,Repeat=1,Character=c}).ToArray());
                    Check(Read(terminal)==new KeyEvent(key),"Encoded "+key);
                }
                Inject(new Record{Type=1,KeyDown=1,Repeat=3,VirtualKey=65,Character='a'});
                for(int i=0;i<3;i++)Check(Read(terminal)==new KeyEvent("a","a"),"Repeat "+i);
                Inject(new Record{Type=2,X=4,Y=4,Buttons=1});
                Check(Read(terminal).Mouse?.Action==MouseAction.Press,"Native mouse");
            }
            Check(GetConsoleMode(input,out uint afterIn)&&beforeIn==afterIn,"Input mode restored");
            Check(GetConsoleMode(output,out uint afterOut)&&beforeOut==afterOut,"Output mode restored");
            Check(Console.OutputEncoding.CodePage==encoding.CodePage,"Output encoding restored");
            Console.WriteLine($"PASS {checks} native Windows console API checks");return 0;
        }
        catch(Exception ex){results.Add(new{name="Native Windows console run",status="FAIL",error=ex.ToString()});Console.Error.WriteLine("FAIL "+ex);return 1;}
        finally{if(reportPath!=null){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);File.WriteAllText(reportPath,System.Text.Json.JsonSerializer.Serialize(new{schema=1,results}));}}
    }
}
