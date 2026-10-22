# HTML: offline working guide

Original quick reference for RudyEdit. Search by typing in the popup; use PgUp/PgDn to scroll. This is a practical introduction, not the complete upstream language reference.

## Semantic documents

Use meaningful elements, a document language, and an informative title. HTML describes structure; CSS supplies presentation.

Example 1:
```html
<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>Notes</title></head>
<body><main><h1>Notes</h1><p>Welcome, Rudy.</p></main></body></html>
```

Example 2:
```html
<nav aria-label="Main"><a href="/">Home</a></nav>
<article><h2>Recipe</h2><p>Warm cider.</p></article>
```

## Forms and labels

Every input needs an associated label. Browser validation improves usability, but the server must validate submitted values too.

Example 1:
```html
<form method="post" action="/notes">
  <label for="title">Title</label>
  <input id="title" name="title" required maxlength="120">
  <button type="submit">Save</button>
</form>
```

Example 2:
```html
<label for="count">Servings</label>
<input id="count" name="count" type="number" min="1" value="2">
```

## Links and media

Use links for navigation and buttons for actions. Describe informative images with alt text and give decorative images an empty alt value.

Example 1:
```html
<a href="guide.html">Read the guide</a>
<img src="cider.jpg" alt="A mug of hot apple cider" width="400" height="300">
```

Example 2:
```html
<button type="button" id="more">More details</button>
<script src="app.js" defer></script>
```

## Previewing websites

RudyEdit colours HTML source and offers keyword/symbol completion. Open the HTML file in a browser for full rendering; the Markdown preview is not a browser.

Example 1:
```html
python3 -m http.server 8000
# Visit http://localhost:8000 in your browser.
```

Example 2:
```html
Connect vscode-html-language-server for richer completion and diagnostics.
```

## Common workflow

Save the file with the proper extension. Press F3 for Development mode, Ctrl+Space for local completion, and F1 for editor commands. Install and explicitly connect a compatible language server for semantic completion and diagnostics. Check tool availability with rudyedit --doctor. The embedded console is a command runner with redirected input/output, rather than a full terminal emulator.

Upstream reference (online): https://html.spec.whatwg.org/
