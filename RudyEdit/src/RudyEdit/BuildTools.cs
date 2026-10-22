using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
namespace RudyEdit;
public sealed record ToolCommand(string Executable,string[] Args,string WorkingDirectory);
public sealed record Diagnostic(string File,int Line,int Column,string Message);
public static class BuildTools
{
    public static string? Which(string file)
    {
        if(Path.IsPathRooted(file))return File.Exists(file)?file:null;
        foreach(string dir in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator))
        foreach(string ext in OperatingSystem.IsWindows()?new[]{".exe",".cmd",".bat",""}:new[]{""})
        {string p=Path.Combine(dir,file+ext);if(File.Exists(p))return p;}return null;
    }
    public static void Doctor()
    {
        Console.WriteLine("RudyEdit 2.0.5 | "+System.Runtime.InteropServices.RuntimeInformation.OSDescription);
        Console.WriteLine("Error log: "+ErrorLog.FilePath);
        Console.WriteLine("Config: "+Settings.ConfigDirectory+"\nToolchains are used only when you request build/run/debug:");
        foreach(var tool in new[]{"dotnet","gcc","python3","python","cargo","rustc","gdb","netcoredbg","clangd","rust-analyzer","pylsp","csharp-ls"})Console.WriteLine($"{tool,-16} {Which(tool)??"not on PATH"}");
    }
    public static string Python=>Which("python3")!=null?"python3":"python";
    public static string? FindProject(string path,string pattern)
    {
        var dir=new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path))!);
        while(dir!=null){var file=dir.EnumerateFiles(pattern).OrderBy(f=>f.Name).FirstOrDefault();if(file!=null)return file.FullName;dir=dir.Parent;}return null;
    }
    public static ToolCommand Plan(string file,bool run)
    {
        string dir=Path.GetDirectoryName(file)!,lang=Languages.Detect(file);
        string? project;
        switch(lang)
        {
            case "csharp": project=FindProject(file,"*.csproj")??throw new IOException("No .csproj found above this file. Create one with dotnet new console in the embedded console.");return new("dotnet",run?["run","--project",project]:["build",project,"--nologo"],Path.GetDirectoryName(project)!);
            case "python":return new(Python,run?["-u",file]:["-m","py_compile",file],dir);
            case "rust":project=FindProject(file,"Cargo.toml")??throw new IOException("No Cargo.toml found. Use cargo init in the console.");return new("cargo",[run?"run":"build","--manifest-path",project],Path.GetDirectoryName(project)!);
            case "c":string output=Path.Combine(dir,Path.GetFileNameWithoutExtension(file)+(OperatingSystem.IsWindows()?".exe":".out"));return run?new(output,[],dir):new("gcc",["-g","-O0","-Wall","-Wextra",file,"-o",output],dir);
            default:throw new IOException("Build/run is supported for C#, C, Python, and Rust. Use the embedded console for other project tools.");
        }
    }
    public static Diagnostic? ParseDiagnostic(string text,string cwd)
    {
        var m=Regex.Match(text,@"^(.+?)\((\d+),(\d+)\):\s*(.+)$");
        if(!m.Success)m=Regex.Match(text,@"^(.+?):(\d+):(\d+):\s*(.+)$");
        if(!m.Success)return null;
        if(!int.TryParse(m.Groups[2].Value,out int line)||!int.TryParse(m.Groups[3].Value,out int column))return null;
        try{return new(Path.GetFullPath(m.Groups[1].Value.Trim(),cwd),line,column,m.Groups[4].Value);}
        catch(Exception ex)when(ex is ArgumentException or NotSupportedException or IOException){ErrorLog.Write(ex,"Parse compiler diagnostic path");return null;}
    }
}
public sealed class ProcessRunner:IDisposable
{
    public Process? Process{get;private set;}
    public bool Active=>Process is {HasExited:false};
    public ConcurrentQueue<string> Output{get;}=new(); public ConcurrentQueue<Diagnostic> Diagnostics{get;}=new();
    public event Action<int>? Exited;
    Action? markStopped;
    public void Start(ToolCommand command)
    {
        if(Active)throw new IOException("A command is already running. Stop it first.");
        var info=new ProcessStartInfo(command.Executable){WorkingDirectory=command.WorkingDirectory,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true,UseShellExecute=false,CreateNoWindow=true};
        foreach(var arg in command.Args)info.ArgumentList.Add(arg);
        Process?.Dispose();Process=null;
        var launched=new Process{StartInfo=info,EnableRaisingEvents=true};bool stopped=false;markStopped=()=>stopped=true;
        void Line(string? s){if(s==null)return;Output.Enqueue(s);var d=BuildTools.ParseDiagnostic(s,command.WorkingDirectory);if(d!=null){Diagnostics.Enqueue(d);ErrorLog.WriteMessage("Compiler diagnostic",s,"DIAGNOSTIC");}}
        launched.OutputDataReceived+=(_,e)=>Line(e.Data);launched.ErrorDataReceived+=(_,e)=>{if(e.Data!=null)ErrorLog.WriteMessage("Tool stderr: "+command.Executable,e.Data,"STDERR");Line(e.Data);};
        launched.Exited+=(_,_)=>{try{Output.Enqueue("[exit "+launched.ExitCode+"]");if(launched.ExitCode!=0&&!stopped)ErrorLog.WriteMessage("Tool exit: "+command.Executable,"Exit code "+launched.ExitCode);Exited?.Invoke(launched.ExitCode);}catch(InvalidOperationException){}};
        try
        {
            if(!launched.Start())throw new IOException("Could not start command.");
            Process=launched;
            launched.BeginOutputReadLine();launched.BeginErrorReadLine();
        }
        catch(Exception ex)
        {
            ErrorLog.Write(ex,"Start tool: "+command.Executable);
            try{if(Process!=null&&!launched.HasExited)launched.Kill(true);}catch(InvalidOperationException){}
            launched.Dispose();Process=null;throw;
        }
        Output.Enqueue("$ "+command.Executable+" "+string.Join(' ',command.Args));
    }
    public void Shell(string text,string folder)=>Start(OperatingSystem.IsWindows()?new("cmd.exe",["/d","/s","/c",text],folder):new("/bin/sh",["-c",text],folder));
    public void Input(string text){if(!Active)throw new IOException("No running process.");Process!.StandardInput.WriteLine(text);Process.StandardInput.Flush();}
    public void Stop(){try{if(Active){markStopped?.Invoke();Process!.Kill(true);}}catch(InvalidOperationException){}}
    public void Dispose(){try{Stop();}finally{Process?.Dispose();Process=null;}}
}
