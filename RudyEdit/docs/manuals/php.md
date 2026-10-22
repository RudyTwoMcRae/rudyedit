# PHP: offline working guide

Original quick reference for RudyEdit. Search by typing in the popup; use PgUp/PgDn to scroll. This is a practical introduction, not the complete upstream language reference.

## Variables and types

PHP code begins with <?php. Strict types helps function calls follow declared scalar parameter types. Escape dynamic content for the output context.

Example 1:
```php
<?php
declare(strict_types=1);
function greet(string $name): string { return "Hello, $name!"; }
echo htmlspecialchars(greet("Rudy"), ENT_QUOTES, "UTF-8");
```

Example 2:
```php
<?php
$notes = ["Cider", "Dinner"];
foreach ($notes as $note) { echo htmlspecialchars($note) . "\n"; }
```

## Classes and namespaces

Namespaces organize reusable code. Dependency managers such as Composer can autoload classes.

Example 1:
```php
<?php
namespace App;
final class Note {
    public function __construct(public string $title) {}
}
```

Example 2:
```php
composer install
php -l index.php
```

## Database access

Use prepared statements with bound values; never concatenate user input into SQL. Configure credentials outside source control.

Example 1:
```php
<?php
$stmt = $pdo->prepare("SELECT title FROM notes WHERE id = :id");
$stmt->execute(["id" => $id]);
$note = $stmt->fetch(PDO::FETCH_ASSOC);
```

Example 2:
```php
<?php
try { $pdo->beginTransaction(); /* validated updates */ $pdo->commit(); }
catch (Throwable $e) { if ($pdo->inTransaction()) $pdo->rollBack(); throw $e; }
```

## Tooling and CodeIgniter

PHP syntax colouring and local completion are built in. Intelephense can provide richer language features. The related CodeIgniter guide follows this guide in the in-app manual.

Example 1:
```php
php -S localhost:8000
# Development server; use a suitable production web server for deployment.
```

Example 2:
```php
composer test
php spark routes
```

## Common workflow

Save the file with the proper extension. Press F3 for Development mode, Ctrl+Space for local completion, and F1 for editor commands. Install and explicitly connect a compatible language server for semantic completion and diagnostics. Check tool availability with rudyedit --doctor. The embedded console is a command runner with redirected input/output, rather than a full terminal emulator.

Upstream reference (online): https://www.php.net/manual/en/
