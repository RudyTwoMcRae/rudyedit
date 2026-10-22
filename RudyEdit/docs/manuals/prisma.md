# Prisma ORM: offline working guide

Original quick reference for RudyEdit. Search by typing in the popup; use PgUp/PgDn to scroll. This is a practical introduction, not the complete upstream language reference.

## Schema models

Prisma schema files describe models, fields, relations, and database configuration. Provider and client-generator setup depend on the installed Prisma major version; follow that version’s documentation.

Example 1:
```prisma
model Note {
  id    Int    @id @default(autoincrement())
  title String
}
```

Example 2:
```prisma
model Tag {
  id   Int    @id @default(autoincrement())
  name String @unique
}
```

## Relations and indexes

Define both sides of a relation and the referencing scalar field. Add indexes for common lookup paths; inspect generated SQL before applying migrations.

Example 1:
```prisma
model Author {
  id Int @id @default(autoincrement())
  notes Note[]
}
model Note {
  id Int @id @default(autoincrement())
  authorId Int
  author Author @relation(fields: [authorId], references: [id])
  @@index([authorId])
}
```

Example 2:
```prisma
npx prisma validate
npx prisma format
```

## Migrations and generated client

Generate a client after schema changes. Use development migrations on a development database and reviewed migration deployment for production.

Example 1:
```prisma
npx prisma migrate dev --name add_notes
npx prisma generate
```

Example 2:
```prisma
npx prisma migrate deploy
# Configure the database URL for your installed Prisma version.
```

## Queries and tooling

Use the generated client and transactions for coordinated updates. This conceptual query assumes a client instance configured by your application.

Example 1:
```prisma
const notes = await prisma.note.findMany({ where: { title: { contains: "cider" } } });
```

Example 2:
```prisma
await prisma.$transaction(async tx => {
  await tx.note.create({ data: { title: "Cider" } });
});
```

## Common workflow

Save the file with the proper extension. Press F3 for Development mode, Ctrl+Space for local completion, and F1 for editor commands. Install and explicitly connect a compatible language server for semantic completion and diagnostics. Check tool availability with rudyedit --doctor. The embedded console is a command runner with redirected input/output, rather than a full terminal emulator.

Upstream reference (online): https://www.prisma.io/docs
