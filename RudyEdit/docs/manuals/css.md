# CSS: offline working guide

Original quick reference for RudyEdit. Search by typing in the popup; use PgUp/PgDn to scroll. This is a practical introduction, not the complete upstream language reference.

## Selectors and the cascade

Rules select elements and assign properties. More specific selectors and later rules can override earlier declarations. Prefer a small, predictable set of classes.

Example 1:
```css
body { color: #243044; background: #f8fafc; font-family: system-ui; }
.note { padding: 1rem; border: 1px solid #ccd5e0; }
```

Example 2:
```css
.note:hover { border-color: #336699; }
button:focus-visible { outline: 3px solid #ffb000; }
```

## Box model and layout

Padding is inside the border; margin is outside. Border-box sizing includes padding and borders in the declared width.

Example 1:
```css
* { box-sizing: border-box; }
.card { width: 20rem; padding: 1rem; margin-block: 1rem; }
```

Example 2:
```css
.toolbar { display: flex; gap: .5rem; align-items: center; }
.grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(15rem, 1fr)); gap: 1rem; }
```

## Responsive and accessible styling

Use flexible units and layouts. Respect reduced motion and keep text/background contrast legible.

Example 1:
```css
@media (max-width: 40rem) {
  .toolbar { flex-direction: column; align-items: stretch; }
}
```

Example 2:
```css
@media (prefers-reduced-motion: reduce) {
  .card { transition: none; }
}
```

## Variables and tooling

Custom properties centralize repeated design values. CSS language-server support is optional; compile SCSS with its own toolchain in the console. 

Example 1:
```css
:root { --accent: #2365a0; --space: 1rem; }
button { background: var(--accent); padding: var(--space); }
```

Example 2:
```css
F10 → Build → Connect Language Server (Development mode).
Server: vscode-css-language-server --stdio
```

## Common workflow

Save the file with the proper extension. Press F3 for Development mode, Ctrl+Space for local completion, and F1 for editor commands. Install and explicitly connect a compatible language server for semantic completion and diagnostics. Check tool availability with rudyedit --doctor. The embedded console is a command runner with redirected input/output, rather than a full terminal emulator.

Upstream reference (online): https://www.w3.org/Style/CSS/
