# C: offline working guide

Original quick reference for RudyEdit. Search by typing in the popup; use PgUp/PgDn to scroll. This is a practical introduction, not the complete upstream language reference.

## Compile and link

A C source file is compiled and linked into an executable. RudyEdit builds the current C file with gcc, debug symbols, and warnings. Multi-file projects can use make in the console.

Example 1:
```c
#include <stdio.h>
int main(void) {
    puts("Hello, Rudy!");
    return 0;
}
```

Example 2:
```c
gcc -g -O0 -Wall -Wextra main.c helper.c -o app
```

## Pointers and ownership

A pointer stores an address. Check allocations, stay within bounds, and free allocated storage exactly once.

Example 1:
```c
#include <stdlib.h>
int *values = calloc(10, sizeof *values);
if (values == NULL) return 1;
values[0] = 42;
free(values);
```

Example 2:
```c
int value = 7;
int *ptr = &value;
printf("%d\n", *ptr);
```

## Functions and structs

Use prototypes in header files and implementations in source files. Pass array lengths explicitly.

Example 1:
```c
int add(int a, int b) { return a + b; }
struct Point { double x; double y; };
```

Example 2:
```c
size_t count_positive(const int *items, size_t count) {
    size_t result = 0;
    for (size_t i = 0; i < count; ++i)
        if (items[i] > 0) ++result;
    return result;
}
```

## Input and debugging

Use bounded reads and validate conversions. Compile with -g and install gdb for F5 stepping. The built-in default C build handles one source file.

Example 1:
```c
char name[80];
if (fgets(name, sizeof name, stdin) != NULL)
    printf("Hello, %s", name);
```

Example 2:
```c
F6 builds main.out (main.exe on Windows).
F5 selects the executable.
F9 toggles source breakpoints; restart gdb to apply changes made during a session.
```

## Common workflow

Save the file with the proper extension. Press F3 for Development mode, Ctrl+Space for local completion, and F1 for editor commands. Install and explicitly connect a compatible language server for semantic completion and diagnostics. Check tool availability with rudyedit --doctor. The embedded console is a command runner with redirected input/output, rather than a full terminal emulator.

Upstream reference (online): https://en.cppreference.com/w/c.html
