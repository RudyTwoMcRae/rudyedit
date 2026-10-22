using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace RudyEdit;
public sealed class JsonRpcProcess:IDisposable
{
    readonly Process process; readonly SemaphoreSlim writer=new(1,1);
    readonly ConcurrentDictionary<int,TaskCompletionSource<JsonElement>> requests=new();
    readonly CancellationTokenSource cancel=new();int sequence;
    public event Action<JsonElement>? Message;
    public event Action<string>? Log;
    public bool Dap{get;}
    public JsonRpcProcess(string command,IEnumerable<string> args,string folder,bool dap=false)
    {
        Dap=dap;var info=new ProcessStartInfo(command){WorkingDirectory=folder,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
        foreach(var a in args)info.ArgumentList.Add(a);process=Process.Start(info)??throw new IOException("Could not start "+command);
        process.ErrorDataReceived+=(_,e)=>{if(e.Data!=null){ErrorLog.WriteMessage("Protocol stderr: "+command,e.Data,"STDERR");Log?.Invoke(e.Data);}};process.BeginErrorReadLine();_ = ReadLoop();
    }
    public async Task<JsonElement> Request(string method,object? args=null)
    {
        int id=Interlocked.Increment(ref sequence);var tcs=new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);requests[id]=tcs;
        try { await Send(Dap?new{seq=id,type="request",command=method,arguments=args}:(object)new{jsonrpc="2.0",id,method,@params=args});return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(12),cancel.Token); }
        catch(TimeoutException ex){throw new IOException($"Tool request '{method}' timed out. Check the tool version, PATH, and debugger permissions.",ex);}
        finally{requests.TryRemove(id,out _);}
    }
    public Task Notify(string method,object? args=null)=>Send(new{jsonrpc="2.0",method,@params=args});
    async Task Send(object message)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(message);var header=Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
        await writer.WaitAsync(cancel.Token);try{await process.StandardInput.BaseStream.WriteAsync(header,cancel.Token);await process.StandardInput.BaseStream.WriteAsync(bytes,cancel.Token);await process.StandardInput.BaseStream.FlushAsync(cancel.Token);}finally{writer.Release();}
    }
    async Task ReadLoop()
    {
        try
        {
            var stream=process.StandardOutput.BaseStream;var one=new byte[1];
            while(!cancel.IsCancellationRequested)
            {
                var header=new List<byte>();while(true){int n=await stream.ReadAsync(one,cancel.Token);if(n==0)throw new EndOfStreamException();header.Add(one[0]);int count=header.Count;if(count>=4&&header[count-4]==13&&header[count-3]==10&&header[count-2]==13&&header[count-1]==10)break;if(count>8192)throw new IOException("Invalid protocol header.");}
                var m=Regex.Match(Encoding.ASCII.GetString(header.ToArray()),@"Content-Length:\s*(\d+)",RegexOptions.IgnoreCase);if(!m.Success)throw new IOException("No Content-Length header.");int length=int.Parse(m.Groups[1].Value);if(length>16*1024*1024)throw new IOException("Protocol message too large.");
                var bytes=new byte[length];await stream.ReadExactlyAsync(bytes,cancel.Token);using var json=JsonDocument.Parse(bytes);var root=json.RootElement.Clone();
                bool response=Dap?root.TryGetProperty("request_seq",out _):root.TryGetProperty("id",out _)&&!root.TryGetProperty("method",out _);
                if(response)
                {
                    int id=root.GetProperty(Dap?"request_seq":"id").GetInt32();
                    if(requests.TryGetValue(id,out var req))
                    {
                        if(root.TryGetProperty("error",out var error)){ErrorLog.WriteMessage("Protocol error response",error.ToString());req.TrySetException(new IOException(error.ToString()));}
                        else if(Dap&&root.TryGetProperty("success",out var ok)&&!ok.GetBoolean()){string failure=root.TryGetProperty("message",out var msg)?msg.ToString():"Debugger request failed.";ErrorLog.WriteMessage("Debugger error response",failure);req.TrySetException(new IOException(failure));}
                        else req.TrySetResult(root.TryGetProperty(Dap?"body":"result",out var body)?body.Clone():default);
                    }
                }
                else if(!Dap&&root.TryGetProperty("id",out var reqId)&&root.TryGetProperty("method",out var method))
                {
                    object? result=method.GetString()=="workspace/configuration"&&root.TryGetProperty("params",out var pars)?pars.GetProperty("items").EnumerateArray().Select(_=>new{}).ToArray():null;
                    await Send(new{jsonrpc="2.0",id=reqId.Clone(),result});
                }
                else Message?.Invoke(root);
            }
        }
        catch(Exception ex)
        {foreach(var p in requests.Values)p.TrySetException(new IOException("Protocol process closed: "+ex.Message));if(!cancel.IsCancellationRequested){ErrorLog.Write(ex,"Protocol read loop");Log?.Invoke(ex.Message);}}
    }
    public void Dispose(){cancel.Cancel();try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}process.Dispose();}
}
public sealed record Completion(string Label,string Insert,int? Start=null,int? Length=null);
public sealed class LanguageServer:IDisposable
{
    readonly JsonRpcProcess rpc; readonly Dictionary<string,int> versions=new();
    public ConcurrentQueue<Diagnostic> Diagnostics{get;}=new();
    public ConcurrentQueue<string> Output{get;}=new();
    public string Language{get;}
    public LanguageServer(string language,string folder)
    {
        Language=language;
        using var config=JsonDocument.Parse(File.ReadAllText(Path.Combine(Settings.ConfigDirectory,"languages.json")));
        if(!config.RootElement.TryGetProperty(language,out var entry))throw new IOException("No server configured for "+language+" in languages.json.");
        rpc=new(entry.GetProperty("command").GetString()!,entry.GetProperty("args").EnumerateArray().Select(x=>x.GetString()!),folder);rpc.Log+=s=>Output.Enqueue(s);
        rpc.Message+=m=>
        {
            if(m.TryGetProperty("method",out var method)&&method.GetString()=="textDocument/publishDiagnostics")
            {
                var p=m.GetProperty("params");string file=new Uri(p.GetProperty("uri").GetString()!).LocalPath;
                // Empty sentinel removes the previous diagnostics for this file.
                Diagnostics.Enqueue(new(file,0,0,""));
                foreach(var d in p.GetProperty("diagnostics").EnumerateArray()){var pos=d.GetProperty("range").GetProperty("start");string diagnostic=d.GetProperty("message").GetString()??"";int line=pos.GetProperty("line").GetInt32()+1,column=pos.GetProperty("character").GetInt32()+1;
                Diagnostics.Enqueue(new(file,line,column,diagnostic));
                if(!d.TryGetProperty("severity",out var severity)||severity.GetInt32()<=2)ErrorLog.WriteMessage("Language server diagnostic",$"{file}:{line}:{column}: {diagnostic}",severity.ValueKind==JsonValueKind.Number&&severity.GetInt32()==2?"WARNING":"ERROR");}
            }
        };
        try{rpc.Request("initialize",new{processId=Environment.ProcessId,rootUri=new Uri(Path.GetFullPath(folder)+Path.DirectorySeparatorChar).AbsoluteUri,capabilities=new{textDocument=new{completion=new{completionItem=new{snippetSupport=false}},synchronization=new{didSave=true}}}}).GetAwaiter().GetResult();rpc.Notify("initialized",new{}).GetAwaiter().GetResult();}
        catch{rpc.Dispose();throw;}
    }
    public void Sync(Document d)
    {
        if(d.FilePath==null)return;string uri=new Uri(d.FilePath).AbsoluteUri;
        if(!versions.ContainsKey(uri)){versions[uri]=1;rpc.Notify("textDocument/didOpen",new{textDocument=new{uri,languageId=d.Language=="react"?(Path.GetExtension(d.FilePath)==".tsx"?"typescriptreact":"javascriptreact"):d.Language,version=1,text=d.Text}}).GetAwaiter().GetResult();}
        else{int version=++versions[uri];rpc.Notify("textDocument/didChange",new{textDocument=new{uri,version},contentChanges=new[]{new{text=d.Text}}}).GetAwaiter().GetResult();}
    }
    public List<Completion> Complete(Document d)
    {
        Sync(d);var pos=d.Position;var data=rpc.Request("textDocument/completion",new{textDocument=new{uri=new Uri(d.FilePath!).AbsoluteUri},position=new{line=pos.Row,character=pos.Column}}).GetAwaiter().GetResult();
        if(data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)return [];
        var array=data.ValueKind==JsonValueKind.Array?data:data.GetProperty("items");var list=new List<Completion>();
        foreach(var item in array.EnumerateArray().Take(150))
        {
            string label=item.GetProperty("label").GetString()??"",insert=item.TryGetProperty("insertText",out var v)?v.GetString()??label:label;int? start=null,length=null;
            if(item.TryGetProperty("textEdit",out var edit)&&edit.TryGetProperty("range",out var range))
            {
                insert=edit.GetProperty("newText").GetString()??label;int Offset(JsonElement e)=>d.Lines.Take(e.GetProperty("line").GetInt32()).Sum(x=>x.Length+1)+e.GetProperty("character").GetInt32();start=Offset(range.GetProperty("start"));length=Offset(range.GetProperty("end"))-start;
            }
            if(item.TryGetProperty("insertTextFormat",out var format)&&format.GetInt32()==2)insert=Regex.Replace(Regex.Replace(insert,@"\$\{\d+:([^}]*)\}","$1"),@"\$\d+|\$\{\d+\}","");
            list.Add(new(label,insert,start,length));
        }
        return list;
    }
    public void Dispose()=>rpc.Dispose();
}
