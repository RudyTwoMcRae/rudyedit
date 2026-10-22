using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace RudyEdit;
public sealed record DebugEvent(string Kind,string Text="",string? File=null,int Line=0);
public sealed class DebuggerSession:IDisposable
{
    Process? process;JsonRpcProcess? dap;int threadId,frameId;string kind="",file="";
    public bool Active{get;private set;} public bool Paused{get;private set;}
    public ConcurrentQueue<DebugEvent> Events{get;}=new();
    public void Start(string source,string? dll,Dictionary<string,HashSet<int>> breakpoints)
    {
        file=source;kind=Languages.Detect(source);Active=true;
        try
        {
            if(kind=="python")
            {
                StartLines(BuildTools.Python,["-u",Path.Combine(AppContext.BaseDirectory,"tools","python_debugger.py"),source],Path.GetDirectoryName(source)!,line=>
                {
                    try{using var j=JsonDocument.Parse(line);var e=j.RootElement;string type=e.GetProperty("event").GetString()!;if(type is "error" or "stderr")ErrorLog.WriteMessage("Python debugger",e.GetProperty("text").GetString()??"",type=="stderr"?"STDERR":"ERROR");if(type=="stopped"){Paused=true;Events.Enqueue(new("stopped",e.GetProperty("locals").ToString(),e.GetProperty("file").GetString(),e.GetProperty("line").GetInt32()));}else Events.Enqueue(new(type,e.TryGetProperty("text",out var text)?text.GetString()??"":""));}catch(JsonException){Events.Enqueue(new("output",line));}
                });
                SendPython("breakpoints",breakpoints:breakpoints);return;
            }
            if(kind is "c" or "rust")
            {
                string target=dll??throw new IOException("Choose a built executable for gdb.");
                StartLines("gdb",["--quiet","--interpreter=mi2",target],Path.GetDirectoryName(source)!,line=>
                {
                    if(line.StartsWith("^error",StringComparison.Ordinal))ErrorLog.WriteMessage("gdb error",line);
                    if(line.StartsWith("*stopped",StringComparison.Ordinal))
                    {
                        if(line.Contains("exited")){Active=false;Events.Enqueue(new("terminated",line));return;}
                        Paused=true;var f=Regex.Match(line,"fullname=\"((?:\\\\.|[^\"])*)\"");var l=Regex.Match(line,"line=\"(\\d+)\"");Events.Enqueue(new("stopped",line,f.Success?Regex.Unescape(f.Groups[1].Value):file,l.Success?int.Parse(l.Groups[1].Value):0));
                    }
                    else if(line.Contains("variables=["))Events.Enqueue(new("variables",line));else if(!line.EndsWith("(gdb) "))Events.Enqueue(new("output",line));
                });
                Write("-gdb-set mi-async on");foreach(var bp in breakpoints)foreach(int line in bp.Value)Write("-break-insert "+Quote(bp.Key+":"+line));Write("-exec-run --start");return;
            }
            if(kind=="csharp")
            {
                string adapter=Environment.GetEnvironmentVariable("RUDYEDIT_NETCOREDBG")??"netcoredbg";
                dap=new(adapter,["--interpreter=vscode"],Path.GetDirectoryName(source)!,true);
                var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                dap.Log+=s=>Events.Enqueue(new("output",s));
                dap.Message+=m=>
                {
                    if(!m.TryGetProperty("event",out var e))return;string ev=e.GetString()??"";var b=m.TryGetProperty("body",out var body)?body:default;
                    if(ev=="initialized")ready.TrySetResult();
                    if(ev=="stopped"){threadId=b.GetProperty("threadId").GetInt32();Paused=true;_ = UpdateStack();}
                    if(ev=="output"){string text=b.GetProperty("output").GetString()??"";if(b.TryGetProperty("category",out var category)&&category.GetString()=="stderr")ErrorLog.WriteMessage("Debug target stderr",text,"STDERR");Events.Enqueue(new("output",text));}
                    if(ev is "terminated" or "exited"){Active=false;Events.Enqueue(new("terminated"));}
                };
                dap.Request("initialize",new{adapterID="coreclr",clientID="rudyedit",pathFormat="path",linesStartAt1=true,columnsStartAt1=true,supportsRunInTerminalRequest=false}).GetAwaiter().GetResult();
                var launch=dap.Request("launch",new{program=dll??throw new IOException("Choose the built .NET DLL."),cwd=Path.GetDirectoryName(source),stopAtEntry=true,console="internalConsole",env=new Dictionary<string,string>(),args=Array.Empty<string>(),justMyCode=false});
                ready.Task.WaitAsync(TimeSpan.FromSeconds(12)).GetAwaiter().GetResult();
                foreach(var bp in breakpoints)SetBreakpoints(bp.Key,bp.Value);
                dap.Request("configurationDone",new{}).GetAwaiter().GetResult();launch.GetAwaiter().GetResult();return;
            }
            throw new IOException("Debugging supports C#, C, Python, and Rust.");
        }
        catch(Exception ex){ErrorLog.Write(ex,"Start debugger");Dispose();throw;}
    }
    static string Quote(string s)=>JsonSerializer.Serialize(s);
    void StartLines(string command,string[] args,string folder,Action<string> receive)
    {
        var info=new ProcessStartInfo(command){WorkingDirectory=folder,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};foreach(var a in args)info.ArgumentList.Add(a);
        process=new Process{StartInfo=info,EnableRaisingEvents=true};process.OutputDataReceived+=(_,e)=>{if(e.Data!=null){try{receive(e.Data);}catch(Exception ex){ErrorLog.Write(ex,"Debugger output processing");Events.Enqueue(new("error",ex.Message));}}};process.ErrorDataReceived+=(_,e)=>{if(e.Data!=null){ErrorLog.WriteMessage("Debugger stderr",e.Data,"STDERR");Events.Enqueue(new("output",e.Data));}};process.Exited+=(_,_)=>{Active=false;Events.Enqueue(new("terminated"));};process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();
    }
    void Write(string s){process!.StandardInput.WriteLine(s);process.StandardInput.Flush();}
    void SendPython(string command,string? text=null,Dictionary<string,HashSet<int>>? breakpoints=null)=>Write(JsonSerializer.Serialize(new{command,text,breakpoints}));
    async Task UpdateStack()
    {
        try{var stack=await dap!.Request("stackTrace",new{threadId,startFrame=0,levels=1});var frame=stack.GetProperty("stackFrames")[0];frameId=frame.GetProperty("id").GetInt32();Events.Enqueue(new("stopped",frame.GetProperty("name").GetString()??"",frame.GetProperty("source").GetProperty("path").GetString(),frame.GetProperty("line").GetInt32()));}
        catch(Exception ex){ErrorLog.Write(ex,"Read debugger stack");Events.Enqueue(new("output",ex.Message));}
    }
    public void SetBreakpoints(string path,IEnumerable<int> lines)
    {
        if(dap!=null)dap.Request("setBreakpoints",new{source=new{path},breakpoints=lines.Select(line=>new{line}).ToArray()}).GetAwaiter().GetResult();
        else if(kind=="python")SendPython("breakpoints",breakpoints:new(){{path,lines.ToHashSet()}});
        else Events.Enqueue(new("output","For gdb, restart debugging to apply changed breakpoints."));
    }
    public void Resume(string action)
    {
        if(!Active)throw new IOException("No debug session.");if(!Paused)throw new IOException("The target is running. Wait for a breakpoint or stop the session.");Paused=false;
        if(dap!=null)dap.Request(action switch{"next"=>"next","step"=>"stepIn","out"=>"stepOut",_=>"continue"},new{threadId}).GetAwaiter().GetResult();
        else if(kind=="python")SendPython(action);
        else Write(action switch{"next"=>"-exec-next","step"=>"-exec-step","out"=>"-exec-finish",_=>"-exec-continue"});
    }
    public void Input(string text){if(kind=="python")SendPython("input",text);else throw new IOException("Interactive input during native/.NET debugging is not supported by this adapter console. Use Run for interactive programs.");}
    public void Variables()
    {
        if(!Paused)throw new IOException("Pause the target to inspect variables.");
        if(dap!=null){var scopes=dap.Request("scopes",new{frameId}).GetAwaiter().GetResult();foreach(var scope in scopes.GetProperty("scopes").EnumerateArray()){var variables=dap.Request("variables",new{variablesReference=scope.GetProperty("variablesReference").GetInt32()}).GetAwaiter().GetResult();Events.Enqueue(new("variables",variables.ToString()));}}
        else if(kind=="python")SendPython("variables");else Write("-stack-list-variables --simple-values");
    }
    public void Dispose(){Active=false;Paused=false;dap?.Dispose();dap=null;if(process!=null){try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}process.Dispose();process=null;}}
}
