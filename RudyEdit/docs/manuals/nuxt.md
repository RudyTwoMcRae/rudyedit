# Nuxt: offline framework guide

Nuxt adds routing, server rendering, and application conventions to Vue. Directory layouts vary by major version. Follow the conventions already used in your project.

## Page component

```
<script setup>
const { data: notes } = await useFetch("/api/notes");
</script>
<template>
  <ul><li v-for="note in notes" :key="note.id">{{ note.title }}</li></ul>
</template>
```

## Server API handler

```
// server/api/notes.get.ts
export default defineEventHandler(() => [
  { id: 1, title: "Cider" }
]);
```

## Navigation and scripts

```
<NuxtLink to="/notes">Notes</NuxtLink>

# Console:
npm run dev
npm run build
```

## Editor integration

RudyEdit detects the underlying HTML, CSS, React, Vue, or PHP file type. Local completion includes common framework identifiers; configure an appropriate language server for project-specific suggestions. Build and test through your project’s scripts in the embedded console.

Upstream reference (online): https://nuxt.com/docs
