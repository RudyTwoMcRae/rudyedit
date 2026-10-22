using RudyEdit;

static class ErrorLogTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void InFolder(string root,string name,Action<string> action)
    {
        string? previous=Environment.GetEnvironmentVariable("RUDYEDIT_CONFIG_HOME");string folder=Path.Combine(root,name);
        Environment.SetEnvironmentVariable("RUDYEDIT_CONFIG_HOME",folder);
        try{Directory.CreateDirectory(folder);ErrorLog.Initialize();action(folder);}
        finally{Environment.SetEnvironmentVariable("RUDYEDIT_CONFIG_HOME",previous);}
    }
    static void Until(Func<bool> condition)
    {
        var end=DateTime.UtcNow.AddSeconds(7);while(DateTime.UtcNow<end){if(condition())return;Thread.Sleep(15);}throw new Exception("Timed out waiting for error logging");
    }
    public static void Run(Action<string,Action> test,string root)
    {
        test("Error log records UTC time, context, stack and nested exception",()=>InFolder(root,"log-detail",_=>
        {
            try{throw new InvalidOperationException("outer fault",new IOException("inner fault"));}
            catch(Exception ex){Check(ErrorLog.Write(ex,"Test command"),"Could not log exception");}
            string log=ErrorLog.Read();Check(log.Contains("+00:00 | ERROR | pid=")&&log.Contains("Test command")&&log.Contains("outer fault")&&log.Contains("inner fault")&&log.Contains("ErrorLogTests"),"Incomplete exception record");
        }));
        test("Concurrent error records are complete without lost entries",()=>InFolder(root,"log-concurrent",_=>
        {
            string token=Guid.NewGuid().ToString("N");Parallel.For(0,64,i=>Check(ErrorLog.WriteMessage("Concurrent operation",token+":"+i),"Write failed"));
            string log=ErrorLog.Read();Check(System.Text.RegularExpressions.Regex.Matches(log,token).Count==64,"Lost or duplicated records");
        }));
        test("Error log rotation keeps three backups and a bounded current file",()=>InFolder(root,"log-rotation",_=>
        {
            for(int i=0;i<5;i++){File.WriteAllText(ErrorLog.FilePath,new string('x',(int)ErrorLog.MaxFileBytes));Check(ErrorLog.WriteMessage("Rotation","entry "+i),"Rotation failed");}
            Check(File.Exists(ErrorLog.FilePath+".3")&&!File.Exists(ErrorLog.FilePath+".4")&&new FileInfo(ErrorLog.FilePath).Length<ErrorLog.MaxFileBytes&&ErrorLog.Read().Contains("entry 4"),"Rotation did not bound log files");
        }));
        test("Unwritable configuration path logs to the temporary fallback",()=>
        {
            string? previous=Environment.GetEnvironmentVariable("RUDYEDIT_CONFIG_HOME");string blocked=Path.Combine(root,"blocked-log-path");File.WriteAllText(blocked,"not a folder");
            try
            {
                Environment.SetEnvironmentVariable("RUDYEDIT_CONFIG_HOME",blocked);string marker="fallback-"+Guid.NewGuid().ToString("N");
                Check(ErrorLog.WriteMessage("Fallback",marker),"Fallback write failed");Check(ErrorLog.FilePath==ErrorLog.FallbackPath&&ErrorLog.Read().Contains(marker),"Fallback log unavailable");
            }
            finally{Environment.SetEnvironmentVariable("RUDYEDIT_CONFIG_HOME",previous);}
        });
        test("Tool stderr, stdout diagnostics and nonzero exit are persisted",()=>InFolder(root,"log-tool",folder=>
        {
            using var runner=new ProcessRunner();runner.Start(new(BuildTools.Python,["-u","-c","import sys; print('demo.c:3:4: error: diagnostic-log-marker'); print('stderr-log-marker',file=sys.stderr); sys.exit(7)"],folder));
            Until(()=>ErrorLog.Read().Contains("Exit code 7")&&ErrorLog.Read().Contains("stderr-log-marker")&&ErrorLog.Read().Contains("diagnostic-log-marker"));
            Check(runner.Output.Any(s=>s.Contains("stderr-log-marker"))&&runner.Diagnostics.Any(),"Logging consumed editor output");
        }));
        test("Malformed asynchronous protocol messages are logged immediately",()=>InFolder(root,"log-protocol",folder=>
        {
            using var rpc=new JsonRpcProcess(BuildTools.Python,["-u","-c","import sys,time; sys.stdout.write('Invalid-Header: 1\\r\\n\\r\\n'); sys.stdout.flush(); time.sleep(2)"],folder);
            Until(()=>ErrorLog.Read().Contains("No Content-Length header."));
        }));
        test("Python debugger stderr and exception events are persisted",()=>InFolder(root,"log-python-debugger",folder=>
        {
            string target=Path.Combine(folder,"error_target.py");File.WriteAllText(target,"import sys\nsys.stderr.write('debug-stderr-marker\\n')\nraise RuntimeError('debug-exception-marker')\n");
            using var debugger=new DebuggerSession();debugger.Start(target,null,[]);Until(()=>debugger.Paused);debugger.Resume("continue");
            Until(()=>ErrorLog.Read().Contains("debug-stderr-marker")&&ErrorLog.Read().Contains("debug-exception-marker"));
        }));
        test("Configuration folder launcher preserves paths as a single non-shell argument",()=>
        {
            string path=Path.Combine(root,"config folder with spaces; literal-$value");
            foreach(bool windows in new[]{false,true})
            {var info=ConfigFolder.LaunchInfo(path,windows);Check(!info.UseShellExecute&&info.ArgumentList.Count==1&&info.ArgumentList[0]==Path.GetFullPath(path)&&info.FileName==(windows?"explorer.exe":"xdg-open"),"Unsafe or incorrect folder launch");}
        });
    }
}
