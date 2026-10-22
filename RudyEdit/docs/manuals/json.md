# JSON: offline working guide

Original quick reference for RudyEdit. Search by typing in the popup; use PgUp/PgDn to scroll. This is a practical introduction, not the complete upstream language reference.

## Data syntax

JSON supports objects, arrays, strings, numbers, booleans, and null. Keys and strings require double quotes. Comments and trailing commas are not standard JSON.

Example 1:
```json
{"title":"Cider","servings":2,"tags":["drink"],"archived":false}
```

Example 2:
```json
{"python":{"command":"pylsp","args":[]}}
```

## RudyEdit language servers

languages.json maps detected language identifiers to executable names and argument arrays. Saving the file does not automatically start a server; connect explicitly from Build.

Example 1:
```json
"c": {"command":"clangd","args":["--background-index"]}
```

Example 2:
```json
"python": {"command":"/path/to/pylsp","args":[]}
```

## Common workflow

Save the file with the proper extension. Press F3 for Development mode, Ctrl+Space for local completion, and F1 for editor commands. Install and explicitly connect a compatible language server for semantic completion and diagnostics. Check tool availability with rudyedit --doctor. The embedded console is a command runner with redirected input/output, rather than a full terminal emulator.

Upstream reference (online): https://www.rfc-editor.org/rfc/rfc8259
