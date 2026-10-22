# Vue: offline working guide

Original quick reference for RudyEdit. Search by typing in the popup; use PgUp/PgDn to scroll. This is a practical introduction, not the complete upstream language reference.

## Single-file components

Vue single-file components combine template, script, and styles. script setup exposes bindings to the template.

Example 1:
```vue
<script setup>
import { ref } from "vue";
const count = ref(0);
</script>
<template><button @click="count++">{{ count }}</button></template>
```

Example 2:
```vue
<style scoped>
button { padding: .75rem; }
</style>
```

## Reactive state and computed values

Use ref for a reactive value and computed for derived data. In JavaScript access a ref using .value; templates unwrap refs.

Example 1:
```vue
const items = ref([{ id: 1, title: "Cider" }]);
const query = ref("");
const visible = computed(() => items.value.filter(item => item.title.includes(query.value)));
```

Example 2:
```vue
<input v-model="query" aria-label="Search notes">
<ul><li v-for="item in visible" :key="item.id">{{ item.title }}</li></ul>
```

## Props, events, and lifecycle

Props pass data down and emitted events send actions up. Clean up side effects when components unmount.

Example 1:
```vue
const props = defineProps({ title: String });
const emit = defineEmits(["save"]);
function save() { emit("save", props.title); }
```

Example 2:
```vue
onMounted(() => console.log("mounted"));
onUnmounted(() => console.log("cleanup"));
```

## Project tooling

Connect a Vue language server configured for your project version. Vue and Nuxt server configuration may require a TypeScript SDK; edit languages.json accordingly.

Example 1:
```vue
npm install
npm run dev
```

Example 2:
```vue
npm run build
# Nuxt-specific notes follow in the related Nuxt guide.
```

## Common workflow

Save the file with the proper extension. Press F3 for Development mode, Ctrl+Space for local completion, and F1 for editor commands. Install and explicitly connect a compatible language server for semantic completion and diagnostics. Check tool availability with rudyedit --doctor. The embedded console is a command runner with redirected input/output, rather than a full terminal emulator.

Upstream reference (online): https://vuejs.org/guide/introduction.html
