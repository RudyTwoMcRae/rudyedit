using RudyEdit;

ErrorLog.InstallGlobalHandlers();

if (args.Contains("--test-catalog")) { ReleaseDiagnostics.Catalog(); return 0; }
if (args.Contains("--self-test")) return ReleaseDiagnostics.Run(args);

if (args.Contains("--version")) { Console.WriteLine("RudyEdit 2.0.6"); return 0; }
if (args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine("RudyEdit 2.0.6 — terminal text and code editor\nUsage: rudyedit [-f FILE] [-p FOLDER]\n       rudyedit [FILE]\n       rudyedit --doctor\n       rudyedit --self-test [--report FILE]\n\nAlt+F/E/M/T/V/B/H menus • F10 menu • F1 help • F2 Code/VIM • F3 Text/Development\nCtrl+S save • Ctrl+Shift+W close tab • Ctrl+W close panel • Ctrl+Q quit • Ctrl+Space commands\nCtrl+Alt+C opens the configuration folder\nConfiguration: " + Settings.ConfigDirectory + "\nError log: " + ErrorLog.FilePath);
    return 0;
}
try
{
    if (args.Contains("--doctor")) { BuildTools.Doctor(); return 0; }
    string? file = null, folder = null;
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] is "-f" or "-p")
        {
            if (++i == args.Length) throw new ArgumentException("Missing path after " + args[i - 1]);
            if (args[i - 1] == "-f") file = Path.GetFullPath(args[i]); else folder = Path.GetFullPath(args[i]);
        }
        else if (!args[i].StartsWith('-') && file == null) file = Path.GetFullPath(args[i]);
        else throw new ArgumentException("Unknown argument: " + args[i]);
    }
    if (folder != null && !Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
    if (Console.IsInputRedirected || Console.IsOutputRedirected)
        throw new InvalidOperationException("Open RudyEdit in an interactive terminal (or use --help / --doctor).");
    using var app = new EditorApp(folder ?? Environment.CurrentDirectory, file, folder != null);
    app.Run(); return 0;
}
catch (Exception ex) { bool logged=ErrorLog.Write(ex,"Application startup or fatal error");Console.Error.WriteLine("RudyEdit: " + ex.Message);Console.Error.WriteLine(logged?"Error log: "+ErrorLog.FilePath:"Could not write the error log: "+ErrorLog.LastFailure);return 1; }
