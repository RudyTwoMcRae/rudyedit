# Python: offline working guide

Original quick reference for RudyEdit. Search by typing in the popup; use PgUp/PgDn to scroll. This is a practical introduction, not the complete upstream language reference.

## Scripts and entry points

Indentation defines blocks. Run a script with Python 3. The editor uses Python from PATH; F6 checks syntax and F5 starts its bundled standard-library debugger.

Example 1:
```python
def main():
    print("Hello, Rudy!")

if __name__ == "__main__":
    main()
```

Example 2:
```python
python3 -m venv .venv
# Linux: source .venv/bin/activate
# Windows: .venv\Scripts\activate
python3 hello.py
```

## Collections and iteration

Lists are ordered, dictionaries map keys to values, and sets hold unique values. Avoid changing a collection while iterating over it.

Example 1:
```python
numbers = [1, 2, 3, 4]
squares = [n * n for n in numbers if n % 2 == 0]
for index, value in enumerate(squares, start=1):
    print(index, value)
```

Example 2:
```python
recipe = {"name": "Cider", "minutes": 15}
print(recipe.get("servings", 1))
tags = {"hot", "drink"}
```

## Functions and type hints

Use descriptive names and return values. Type hints document intent; the Python interpreter does not enforce them by itself.

Example 1:
```python
def calories(grams: float, per_100g: float) -> float:
    return grams * per_100g / 100

print(calories(150, 52))
```

Example 2:
```python
def greet(name: str = "Rudy") -> str:
    return f"Hello, {name}!"

print(greet())
```

## Files, JSON, and exceptions

Use context managers so files close even after an error. Specify UTF-8 for portable text. Catch errors you can recover from.

Example 1:
```python
import json
from pathlib import Path
path = Path("notes.json")
path.write_text(json.dumps({"tags": ["work"]}), encoding="utf-8")
data = json.loads(path.read_text(encoding="utf-8"))
```

Example 2:
```python
try:
    number = int(input("Number: "))
except ValueError:
    print("Please enter a whole number.")
```

## Step through a program

The stdlib bdb debugger pauses before executing the highlighted line. Locals describe the preceding execution state. It supports next, into, out, continue, and breakpoints.

Example 1:
```python
value = 1
value += 2
print(value)
# F5 pauses on line 1; F10 pauses on line 2 with value=1.
```

Example 2:
```python
F9 sets a breakpoint.
Build → Variables inspects local values.
View → Focus Console sends input to input().
Shift+F5 stops the target.
```

## Common workflow

Save the file with the proper extension. Press F3 for Development mode, Ctrl+Space for local completion, and F1 for editor commands. Install and explicitly connect a compatible language server for semantic completion and diagnostics. Check tool availability with rudyedit --doctor. The embedded console is a command runner with redirected input/output, rather than a full terminal emulator.

Upstream reference (online): https://docs.python.org/3/tutorial/
