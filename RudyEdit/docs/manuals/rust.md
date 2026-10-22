# Rust: offline working guide

Original quick reference for RudyEdit. Search by typing in the popup; use PgUp/PgDn to scroll. This is a practical introduction, not the complete upstream language reference.

## Cargo projects

Cargo manages dependencies and builds the project. F6 finds the nearest Cargo.toml; Run uses cargo run.

Example 1:
```rust
cargo new hello
cd hello
cargo run
```

Example 2:
```rust
fn main() {
    let name = "Rudy";
    println!("Hello, {name}!");
}
```

## Ownership and borrowing

Each owned value has one owner. Borrowing lets a function access a value without taking ownership. A mutable reference requires exclusive access.

Example 1:
```rust
fn length(value: &str) -> usize { value.len() }
let name = String::from("Rudy");
println!("{}", length(&name));
```

Example 2:
```rust
let mut items = vec![1, 2];
items.push(3);
for item in &items { println!("{item}"); }
```

## Enums and error handling

Use Option for an absent value and Result for fallible operations. The ? operator propagates a compatible error.

Example 1:
```rust
fn read_note() -> std::io::Result<String> {
    let text = std::fs::read_to_string("notes.md")?;
    Ok(text)
}
```

Example 2:
```rust
match "42".parse::<i32>() {
    Ok(number) => println!("{number}"),
    Err(error) => eprintln!("{error}"),
}
```

## Debugging and tests

Build Debug binaries with cargo build, install gdb, and select target/debug/<binary> in F5. Optimized release code may not map cleanly to source lines.

Example 1:
```rust
#[test]
fn adds() { assert_eq!(2 + 3, 5); }
```

Example 2:
```rust
cargo test
cargo fmt
cargo clippy
```

## Common workflow

Save the file with the proper extension. Press F3 for Development mode, Ctrl+Space for local completion, and F1 for editor commands. Install and explicitly connect a compatible language server for semantic completion and diagnostics. Check tool availability with rudyedit --doctor. The embedded console is a command runner with redirected input/output, rather than a full terminal emulator.

Upstream reference (online): https://doc.rust-lang.org/book/
