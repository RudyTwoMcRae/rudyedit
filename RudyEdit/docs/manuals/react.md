# React: offline working guide

Original quick reference for RudyEdit. Search by typing in the popup; use PgUp/PgDn to scroll. This is a practical introduction, not the complete upstream language reference.

## Components and JSX

Components return UI descriptions. Capitalized names refer to components. Use stable identifiers for list keys.

Example 1:
```jsx
export default function Note({ title }) {
  return <article><h2>{title}</h2></article>;
}
```

Example 2:
```jsx
function Notes({ items }) {
  return <ul>{items.map(item => <li key={item.id}>{item.title}</li>)}</ul>;
}
```

## State and events

State changes schedule a render. Update objects and arrays immutably; use updater functions when deriving the next state from the previous value.

Example 1:
```jsx
import { useState } from "react";
function Counter() {
  const [count, setCount] = useState(0);
  return <button onClick={() => setCount(c => c + 1)}>{count}</button>;
}
```

Example 2:
```jsx
const [form, setForm] = useState({ title: "" });
const changeTitle = title => setForm(old => ({ ...old, title }));
```

## Effects and cleanup

Effects synchronize with external systems. Return cleanup functions for subscriptions and timers; derive ordinary render values directly.

Example 1:
```jsx
useEffect(() => {
  const id = setInterval(() => setSeconds(s => s + 1), 1000);
  return () => clearInterval(id);
}, []);
```

Example 2:
```jsx
const visible = items.filter(item => item.title.includes(query));
// Derive this during render instead of mirroring it into state.
```

## Development tools

JSX/TSX are detected automatically. Connect typescript-language-server for project-aware suggestions; use npm scripts in the console. Next.js and Tailwind have related guides.

Example 1:
```jsx
npm install
npm run dev
```

Example 2:
```jsx
npm run build
npm test
```

## Common workflow

Save the file with the proper extension. Press F3 for Development mode, Ctrl+Space for local completion, and F1 for editor commands. Install and explicitly connect a compatible language server for semantic completion and diagnostics. Check tool availability with rudyedit --doctor. The embedded console is a command runner with redirected input/output, rather than a full terminal emulator.

Upstream reference (online): https://react.dev/learn
