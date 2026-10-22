# XML: offline working guide

Original quick reference for RudyEdit. Search by typing in the popup; use PgUp/PgDn to scroll. This is a practical introduction, not the complete upstream language reference.

## Well-formed documents

XML is case-sensitive. Every opened element must close, attributes need quotes, and a document has one root element.

Example 1:
```xml
<?xml version="1.0" encoding="utf-8"?>
<notes><note id="1"><title>Cider</title></note></notes>
```

Example 2:
```xml
<note title="Tom &amp; Jerry">Use &lt; and &gt; for angle brackets.</note>
```

## Namespaces and schemas

Namespaces distinguish vocabularies. A schema can impose structure beyond basic XML well-formedness.

Example 1:
```xml
<ui:Window xmlns:ui="urn:example:ui">
  <ui:Title>Notes</ui:Title>
</ui:Window>
```

Example 2:
```xml
<configuration xmlns="urn:example:config">
  <option name="theme" value="dark" />
</configuration>
```

## RudyEdit shortcuts

Use the supplied KeyboardShortcuts schema: a KeyboardShortcuts root containing Binding elements with Name, Keys, Context, and Description. F1 in that file lists valid command names and examples.

Example 1:
```xml
<KeyboardShortcuts>
  <Binding><Name>save</Name><Keys>Ctrl+k, s</Keys><Context>Global</Context><Description>Save file</Description></Binding>
</KeyboardShortcuts>
```

Example 2:
```xml
<Binding><Name>Go To Next Tab</Name><Keys>Ctrl+k, t, Right Arrow</Keys><Context>Tab</Context><Description>Next tab</Description></Binding>
```

## Tooling and data safety

Use an XML parser rather than text replacement for structured updates. Do not enable external entity resolution when parsing untrusted XML. Syntax colouring is lexical; schema validation requires a suitable server.

Example 1:
```xml
F10 → Help → Keybindings
Ctrl+S reloads a valid map.
```

Example 2:
```xml
Connect a compatible XML language server using languages.json.
```

## Common workflow

Save the file with the proper extension. Press F3 for Development mode, Ctrl+Space for local completion, and F1 for editor commands. Install and explicitly connect a compatible language server for semantic completion and diagnostics. Check tool availability with rudyedit --doctor. The embedded console is a command runner with redirected input/output, rather than a full terminal emulator.

Upstream reference (online): https://www.w3.org/TR/xml/
