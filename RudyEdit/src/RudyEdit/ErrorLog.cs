using System.Globalization;
using System.Text;
namespace RudyEdit;

/// <summary>Local, bounded error logging that cannot turn an application error into a logging crash.</summary>
public static class ErrorLog
{
    public const long MaxFileBytes=5*1024*1024;
    const int Backups=3,MaxEntryChars=65536;
    static readonly object Gate=new();
    static string? activePath,preferredPath;
    static int handlersInstalled;
    public static string? LastFailure {get;private set;}
    public static string FilePath {get{lock(Gate)return preferredPath==Settings.ErrorLogPath&&activePath!=null?activePath:Settings.ErrorLogPath;}}
    public static string FallbackPath=>Path.Combine(Path.GetTempPath(),"RudyEdit-"+string.Concat(Environment.UserName.Select(c=>char.IsLetterOrDigit(c)?c:'_')),"RudyEdit-errors.log");
    public static string Read()
    {
        lock(Gate){using var stream=new FileStream(FilePath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);using var reader=new StreamReader(stream);return reader.ReadToEnd();}
    }
    public static void Initialize()=>WriteMessage("Session","RudyEdit "+typeof(ErrorLog).Assembly.GetName().Version+" started","INFO");
    public static void InstallGlobalHandlers()
    {
        if(Interlocked.Exchange(ref handlersInstalled,1)!=0)return;
        AppDomain.CurrentDomain.UnhandledException+=(_,e)=>WriteMessage("Unhandled exception",e.ExceptionObject.ToString()??"Unknown exception");
        TaskScheduler.UnobservedTaskException+=(_,e)=>Write(e.Exception,"Unobserved background task");
    }
    public static bool Write(Exception error,string context)=>WriteMessage(context,error.ToString());
    public static bool WriteMessage(string context,string detail,string level="ERROR")
    {
        try
        {
            string header=$"{DateTimeOffset.UtcNow.ToString("O",CultureInfo.InvariantCulture)} | {level} | pid={Environment.ProcessId} | {context.Replace('\r',' ').Replace('\n',' ')}";
            if(detail.Length>MaxEntryChars)detail=detail[..MaxEntryChars]+"\n[entry truncated after 65,536 characters]";
            string record=header+Environment.NewLine+detail+Environment.NewLine+Environment.NewLine;
            lock(Gate)
            {
                string preferred=Settings.ErrorLogPath;preferredPath=preferred;
                foreach(string path in new[]{preferred,FallbackPath}.Distinct())
                {
                    try{Append(path,record);activePath=path;LastFailure=null;return true;}
                    catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
                    {LastFailure=ex.Message;}
                }
            }
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {LastFailure=ex.Message;}
        return false;
    }
    static void Append(string path,string record)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        FileStream gate;
        for(int n=0;;n++)
        {
            try{gate=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);break;}
            catch(IOException) when(n<30){Thread.Sleep(10);}
        }
        using(gate)
        {
            byte[] bytes=new UTF8Encoding(false).GetBytes(record);
            if(File.Exists(path)&&new FileInfo(path).Length+bytes.Length>MaxFileBytes)
            {
                for(int i=Backups;i>=1;i--)
                {
                    string source=i==1?path:path+"."+(i-1),destination=path+"."+i;
                    if(File.Exists(source))File.Move(source,destination,true);
                }
            }
            using var file=new FileStream(path,FileMode.Append,FileAccess.Write,FileShare.Read);
            file.Write(bytes);
        }
    }
}
