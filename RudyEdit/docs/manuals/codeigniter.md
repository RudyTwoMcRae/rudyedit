# CodeIgniter: offline framework guide

CodeIgniter 4 organizes requests into routes, controllers, models, and views. Framework APIs here assume version 4. Keep database secrets in environment configuration.

## Routes and controller

```
// app/Config/Routes.php
$routes->get("notes", "Notes::index");

// In a Notes controller extending BaseController:
public function index() { return view("notes", ["title" => "Notes"]); }
```

## Escaped views

```
<h1><?= esc($title) ?></h1>
<?= csrf_field() ?>
```

## Development tools

```
php spark routes
php spark serve
php spark migrate
```

## Editor integration

RudyEdit detects the underlying HTML, CSS, React, Vue, or PHP file type. Local completion includes common framework identifiers; configure an appropriate language server for project-specific suggestions. Build and test through your project’s scripts in the embedded console.

Upstream reference (online): https://codeigniter.com/user_guide/
