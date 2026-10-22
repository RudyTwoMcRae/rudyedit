# C#: offline working guide

Original quick reference for RudyEdit. Search by typing in the popup; use PgUp/PgDn to scroll. This is a practical introduction, not the complete upstream language reference.

## Create and run a console project

C# source belongs in a .NET project. The SDK compiles code; the bundled editor runtime does not include the SDK.

Example 1:
```cs
dotnet new console -n Hello
cd Hello
dotnet run
```

Example 2:
```cs
Console.WriteLine("Hello, Rudy!");
var total = 12.50m * 3;
Console.WriteLine($"Total: {total:C}");
```

## Types and collections

Use decimal for exact financial arithmetic, nullable types for optional values, and generic collections for typed data.

Example 1:
```cs
string name = "Rudy";
int? age = null;
var names = new List<string> { name, "Dave" };
```

Example 2:
```cs
foreach (var item in names)
{
    Console.WriteLine(item.ToUpperInvariant());
}
```

## Functions and classes

Methods declare return and parameter types. Classes group state and behaviour; records are useful for data values.

Example 1:
```cs
static int Add(int a, int b) => a + b;
Console.WriteLine(Add(2, 3));
```

Example 2:
```cs
public record Ingredient(string Name, decimal Calories);
var apple = new Ingredient("Apple", 52m);
```

## Asynchronous work and failures

Await asynchronous operations rather than blocking threads. Catch specific exceptions you can handle. Dispose file streams and other resources.

Example 1:
```cs
try
{
    string text = await File.ReadAllTextAsync("notes.md");
    Console.WriteLine(text);
}
catch (IOException ex) { Console.Error.WriteLine(ex.Message); }
```

Example 2:
```cs
using var reader = new StreamReader("notes.md");
string? line;
while ((line = reader.ReadLine()) != null) Console.WriteLine(line);
```

## Build and debug

F6 builds the nearest csproj. F5 asks for a Debug DLL and uses netcoredbg from PATH. Keep portable PDB files next to the target. Debug stepping is over source lines, which can differ from individual statements.

Example 1:
```cs
dotnet build -c Debug
# Choose bin/Debug/net10.0/Hello.dll in the debugger dialog.
```

Example 2:
```cs
F9: breakpoint
F5: start / continue
F10: step over
F11: step into
Shift+F11: step out
```

## Common workflow

Save the file with the proper extension. Press F3 for Development mode, Ctrl+Space for local completion, and F1 for editor commands. Install and explicitly connect a compatible language server for semantic completion and diagnostics. Check tool availability with rudyedit --doctor. The embedded console is a command runner with redirected input/output, rather than a full terminal emulator.

Upstream reference (online): https://learn.microsoft.com/dotnet/csharp/
