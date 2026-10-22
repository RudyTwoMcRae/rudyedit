# Developer tools

RudyEdit itself includes its .NET runtime and runs offline after installation. Build tools, debuggers, and language servers must be installed separately. No project commands or language servers start merely because a file is opened.

## Manjaro

Use the system package manager for the toolchains you need. Typical package names are `dotnet-sdk`, `gcc`, `python`, `rust`, and `gdb`. Check availability in your configured repositories before installing. For Python LSP, use your distribution's `python-lsp-server` package or install it in a virtual environment and put the server path in languages.json. Rust's rust-analyzer and C's clangd are optional. netcoredbg can be obtained from Samsung's upstream release page. Use a binary compatible with your machine and target .NET version.

## Windows

Install the .NET 10 SDK for C#. Install Python 3 for Python and add it to PATH. C needs a GCC/GDB distribution such as MSYS2's UCRT64 toolchain, with its bin directory on PATH. Rust needs the Rust toolchain; gdb debugging requires a compatible GNU target/debugger, rather than assuming MSVC binaries work with gdb. Samsung netcoredbg supplies the C# DAP adapter. Keep its support files with the executable and add the directory to PATH.

## Language servers

Edit the generated `languages.json` to match tools actually installed. The executable is launched directly with an argument array, never passed through a shell. Use an absolute path if it is not on PATH. Some servers need additional SDK paths or project-specific setup; the JSON defaults are starting commands, not an automated server installer.

| Detected id | Default executable | Purpose |
|---|---|---|
| csharp | csharp-ls | C# semantic completion/diagnostics |
| c | clangd | C completion/diagnostics; compile_commands.json improves projects |
| python | pylsp | Python completion/diagnostics |
| rust | rust-analyzer | Rust project analysis |
| javascript / react | typescript-language-server --stdio | JS/TS/JSX/TSX |
| vue | vue-language-server --stdio | Vue; may need TypeScript SDK configuration |
| html | vscode-html-language-server --stdio | HTML |
| css | vscode-css-language-server --stdio | CSS |
| php | intelephense --stdio | PHP; upstream licensing/features apply |
| prisma | prisma-language-server --stdio | Prisma schema |
| xml | lemminx | XML; install a compatible launcher or configure Java invocation |

Only one server is connected at a time. Opening a new working folder disconnects it. Full document updates and explicit completion are supported. Rename, definition navigation, formatting, code actions, snippets with placeholders, and all other LSP capabilities are not part of this release. Local completion continues to work without servers.

## External references

- .NET publishing: https://learn.microsoft.com/dotnet/core/tools/dotnet-publish
- Windows VT output: https://learn.microsoft.com/windows/console/console-virtual-terminal-sequences
- GDB MI: https://sourceware.org/gdb/current/onlinedocs/gdb.html/GDB_002fMI.html
- Python bdb: https://docs.python.org/3/library/bdb.html
- netcoredbg: https://github.com/Samsung/netcoredbg
- Language Server Protocol: https://microsoft.github.io/language-server-protocol/
- Nerd Fonts: https://github.com/ryanoasis/nerd-fonts
