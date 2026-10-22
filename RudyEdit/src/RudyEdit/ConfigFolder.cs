using System.Diagnostics;
namespace RudyEdit;

public static class ConfigFolder
{
    public static ProcessStartInfo LaunchInfo(string path,bool windows)
    {
        var info=new ProcessStartInfo(windows?"explorer.exe":"xdg-open"){UseShellExecute=false,CreateNoWindow=true};
        info.ArgumentList.Add(Path.GetFullPath(path));
        if(!windows){info.RedirectStandardOutput=true;info.RedirectStandardError=true;}
        return info;
    }
    // File Manager already shows this directory, even on a system with no desktop.
    public static void Open(string path)
    {
        if(!OperatingSystem.IsWindows()){_ = OpenLinux(LaunchInfo(path,false));return;}
        try{using var process=Process.Start(LaunchInfo(path,true))??throw new IOException("Could not start Explorer.");}
        catch(Exception ex){ErrorLog.Write(ex,"Open configuration folder");}
    }
    static async Task OpenLinux(ProcessStartInfo info)
    {
        try
        {
            using var process=Process.Start(info)??throw new IOException("Could not start xdg-open.");
            var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();await stdout;string errors=await stderr;
            if(errors.Length>0)ErrorLog.WriteMessage("Configuration folder opener",errors,"STDERR");
            if(process.ExitCode!=0)ErrorLog.WriteMessage("Configuration folder opener","xdg-open exited with code "+process.ExitCode+". The folder remains available in RudyEdit's File Manager.");
        }
        catch(Exception ex){ErrorLog.Write(ex,"Open configuration folder");}
    }
}
