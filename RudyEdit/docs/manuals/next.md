# Next.js: offline framework guide

Next.js builds React applications with routing and server/client rendering. These examples use the App Router. Match framework setup to the major version installed in your project.

## Server page

```
// app/page.jsx
export default function Page() {
  return <main><h1>Recipe journal</h1></main>;
}
```

## Client interaction

```
"use client";
import { useState } from "react";
export default function Count() {
  const [n, setN] = useState(0);
  return <button onClick={() => setN(v => v + 1)}>{n}</button>;
}
```

## Route handler

```
// app/api/notes/route.js
export async function GET() {
  return Response.json([{ id: 1, title: "Cider" }]);
}
```

## Editor integration

RudyEdit detects the underlying HTML, CSS, React, Vue, or PHP file type. Local completion includes common framework identifiers; configure an appropriate language server for project-specific suggestions. Build and test through your project’s scripts in the embedded console.

Upstream reference (online): https://nextjs.org/docs
