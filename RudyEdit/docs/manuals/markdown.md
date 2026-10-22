# Markdown notes: offline working guide

Original quick reference for RudyEdit. Search by typing in the popup; use PgUp/PgDn to scroll. This is a practical introduction, not the complete upstream language reference.

## Headings and emphasis

Text mode stores ordinary Markdown. Most readers support headings, emphasis, code spans, and lists. The source remains directly editable.

Example 1:
```markdown
# Meeting notes

**Important** and *emphasized* text.

Use `rudyedit` to begin.
```

Example 2:
```markdown
## Actions
- Check the recipe
- Review the build

1. Open a file
2. Save your changes
```

## Links and fenced code

Use descriptive link labels. Fenced code preserves whitespace and can include a language identifier.

Example 1:
```markdown
[Project documentation](https://example.com)

![A descriptive image label](photo.jpg)
```

Example 2:
```markdown
```python
def greet(name):
    print(f"Hello, {name}!")
```
```

## Font, colour, and underline

Markdown has no standard font family, size, or underline syntax. RudyEdit uses HTML spans and u elements; reader support and sanitization vary. The terminal uses a fixed font grid.

Example 1:
```markdown
<u>Underlined note</u>
<span style="color:#cc6633">Coloured text</span>
```

Example 2:
```markdown
<span style="font-family:'FiraCode Nerd Font Mono';font-size:18pt">Title</span>
```

## Tags, spelling, and preview

Tag names are stored in Tags.xml beside Config.cs. Tagged files contain a hidden rudyedit-id comment only. Ctrl+Shift+T toggles the tag bar; Ctrl+Alt+T focuses Add tags and Enter adds comma-separated tags. Ctrl+S saves both the link and XML metadata. Back up Tags.xml with tagged documents. Search Tags scans saved text/Markdown files below the File Manager folder using these identifiers. F7 checks English words; Ctrl+Shift+M opens a terminal preview.

Example 1:
```markdown
<!-- rudyedit-id: fa0f8e2e-d9cc-4561-b020-6a3b2bd16f41 -->

# Cider recipe
```

Example 2:
```markdown
Select text with Shift+arrows.
Ctrl+B: bold
Ctrl+I: italic (legacy terminals may send Tab instead)
Ctrl+U: underline
F10 → Text offers every formatting action.
```

## Common workflow

Save the file with the proper extension. Press F3 for Development mode, Ctrl+Space for local completion, and F1 for editor commands. Install and explicitly connect a compatible language server for semantic completion and diagnostics. Check tool availability with rudyedit --doctor. The embedded console is a command runner with redirected input/output, rather than a full terminal emulator.

Upstream reference (online): https://spec.commonmark.org/
