# Tailwind CSS: offline framework guide

Tailwind expresses styling with utility classes. The utilities available and build configuration depend on the installed major version. Use your project’s own documentation/configuration.

## Utility composition

```
<button class="rounded-lg bg-blue-600 px-4 py-2 font-semibold text-white hover:bg-blue-700">Save</button>
```

## Responsive design

```
<main class="mx-auto grid max-w-5xl gap-4 p-4 md:grid-cols-2">
  <article class="rounded border p-4">Recipe</article>
</main>
```

## Accessible states

```
<input class="border p-2 focus-visible:outline-2 focus-visible:outline-blue-600" aria-label="Title">
```

## Editor integration

RudyEdit detects the underlying HTML, CSS, React, Vue, or PHP file type. Local completion includes common framework identifiers; configure an appropriate language server for project-specific suggestions. Build and test through your project’s scripts in the embedded console.

Upstream reference (online): https://tailwindcss.com/docs
