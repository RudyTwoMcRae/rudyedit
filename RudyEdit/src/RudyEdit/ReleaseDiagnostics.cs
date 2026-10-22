using System.Reflection;
using System.Text.Json;
namespace RudyEdit;

/// <summary>Runs the shipped diagnostics inside this exact published executable.</summary>
static class ReleaseDiagnostics
{
    public static int Run(string[] args)
    {
        string runner=Path.Combine(AppContext.BaseDirectory,"tests","managed","RudyEdit.Tests.dll");
        if(!File.Exists(runner)){Console.Error.WriteLine("Packaged test runner missing. Run tools/stage_release_tests.py after building the tests.");return 2;}
        try
        {
            var entry=Assembly.LoadFrom(runner).EntryPoint??throw new IOException("Test runner has no entry point.");
            return (int)(entry.Invoke(null,[args.Where(a=>a!="--self-test").Append("--from-executable").ToArray()])??1);
        }
        catch(Exception ex){Console.Error.WriteLine(ex is TargetInvocationException?ex.InnerException:ex);return 1;}
    }
    public static void Catalog()=>Console.WriteLine(JsonSerializer.Serialize(Commands.All.DistinctBy(c=>c.Id).Select(c=>new{c.Id,c.Name,c.Menu,c.Shortcut})));
}
