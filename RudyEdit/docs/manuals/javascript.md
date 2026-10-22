# JavaScript: offline working guide

Original quick reference for RudyEdit. Search by typing in the popup; use PgUp/PgDn to scroll. This is a practical introduction, not the complete upstream language reference.

## Bindings, arrays, and objects

Use const by default and let for reassignment. JavaScript values are dynamically typed; avoid relying on implicit coercion.

Example 1:
```javascript
const note = { title: "Cider", tags: ["drink"] };
const titles = [note].map(item => item.title);
console.log(titles);
```

Example 2:
```javascript
const total = [1, 2, 3].reduce((sum, value) => sum + value, 0);
console.log(total);
```

## Functions and modules

Export reusable functions from modules. Keep effects such as network calls separate from pure data transforms.

Example 1:
```javascript
export function calories(grams, per100g) {
  return grams * per100g / 100;
}
```

Example 2:
```javascript
import { calories } from "./nutrition.js";
console.log(calories(150, 52));
```

## Asynchronous requests

Await promises and handle both network failures and unsuccessful HTTP status codes. Do not insert untrusted strings as HTML.

Example 1:
```javascript
async function loadNotes() {
  const response = await fetch("/api/notes");
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  return await response.json();
}
```

Example 2:
```javascript
try { console.log(await loadNotes()); }
catch (error) { console.error(error); }
```

## DOM and project tools

Use event listeners for user actions. Node/npm and the chosen bundler run in the embedded console; the built-in F6 targets C#/C/Python/Rust.

Example 1:
```javascript
document.querySelector("button").addEventListener("click", () => {
  document.querySelector("p").textContent = "Saved";
});
```

Example 2:
```javascript
npm install
npm run build
npm test
```

## Common workflow

Save the file with the proper extension. Press F3 for Development mode, Ctrl+Space for local completion, and F1 for editor commands. Install and explicitly connect a compatible language server for semantic completion and diagnostics. Check tool availability with rudyedit --doctor. The embedded console is a command runner with redirected input/output, rather than a full terminal emulator.

Upstream reference (online): https://tc39.es/ecma262/
