# Contributing to Trading Card Game Tracker

Thank you for your interest in contributing! This guide explains how to set up your environment, follow project conventions, and submit changes.

---

## Table of Contents

1. [Development Setup](#development-setup)
2. [Project Structure](#project-structure)
3. [Coding Standards](#coding-standards)
4. [Running Tests](#running-tests)
5. [Database Migrations](#database-migrations)
6. [Adding a New Game Importer](#adding-a-new-game-importer)
7. [Submitting Changes](#submitting-changes)
8. [Branch Strategy](#branch-strategy)

---

## Development Setup

### Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 9.0 (pinned in `global.json`) |
| Node.js | 20+ |
| pnpm | 9+ (`npm i -g pnpm`) |
| SQLite | Any recent version (bundled via EF Core) |

### 1. Clone and restore

```bash
git clone https://github.com/Xercius/Trading-Card-Game-Tracker.git
cd Trading-Card-Game-Tracker

# Backend
dotnet restore ./api/api.csproj

# Frontend
pnpm install --filter client-vite...
```

### 2. Configure JWT secret (local only)

```bash
dotnet user-secrets set "Jwt:Key" "DevOnly_Minimum_32_Chars_Key_For_Local_Use_1234" \
  --project ./api/api.csproj
```

The API falls back to a deterministic dev key when no secret is configured in `Development` or `Testing` mode.

### 3. Seed test data

```bash
cd api
dotnet run seed   # runs migrations then inserts sample cards/sets; exits without starting the server
```

### 4. Start the servers

```bash
# Terminal 1 – API (HTTPS on :7226, HTTP on :5229)
dotnet run --project ./api/api.csproj --launch-profile "TradingCardApi (HTTPS)"

# Terminal 2 – Vite client (:5173)
pnpm --filter client-vite dev
```

Create `client-vite/.env.local`:

```
VITE_API_BASE=https://localhost:7226/api
```

---

## Project Structure

```
api/                     .NET 9 ASP.NET Core API
  Authentication/        JWT token provider
  Controllers/           Non-feature controllers (ImportExport)
  Data/                  AppDbContext + EF Core migrations
  Features/              Feature folders (Cards, Collections, Decks, …)
    <Feature>/
      <Feature>Controller.cs
      Dtos/
      Mapping/           AutoMapper profiles
      Services/
      Validation/        FluentValidation validators
  Importing/             Per-game card importers (13 total)
  Models/                EF Core entity classes
  Shared/                Cross-cutting utilities
api.Tests/               XUnit integration tests
  Infrastructure/        TestingWebAppFactory (in-memory SQLite)
  Seed/                  Deterministic test data seeder
  Features/              Feature-based test classes
client-vite/             React 19 + TypeScript + Vite frontend
  src/
    app/                 App shell, layout, providers
    components/          Shared UI components (shadcn/ui based)
    features/            Feature modules mirroring API features
    hooks/               Custom React hooks
    lib/                 HTTP client, query keys, utils
    pages/               Route-level page components
    routes/              React Router configuration
    state/               Global context (UserContext)
    types/               Shared TypeScript types
```

---

## Coding Standards

### C# / Backend

- Use `async`/`await` throughout. Pass `CancellationToken` from controller to EF.
- Inject all dependencies via the built-in DI container; no `new` for services.
- DTOs are `record` types (immutable). Map between domain and DTO only via **AutoMapper** profiles.
- Controllers return `ActionResult<T>` (or `IActionResult`) with RFC 7807 `ProblemDetails` for errors.
- Error status codes: `400` (validation), `401` (unauthenticated), `403` (forbidden), `404` (not found), `409` (conflict).
- **Never** call `.ToLower()` for case-insensitive search — use `EF.Functions.Like` or SQLite `NOCASE` collation.
- Reads use `.AsNoTracking()`; writes track entities for change detection.
- All list endpoints accept `page` / `pageSize` (with a maximum cap) and return `X-Total-Count`.
- Use the **Options pattern** (`IOptions<T>` + `ValidateOnStart()`) for configuration sections.
- Structured logging with `EventId`; use `BeginScope` for request/user/deck context.
- Format before committing: `dotnet format ./api/api.sln`

### TypeScript / React / Frontend

- Functional components + hooks only. No class components.
- Strict TypeScript: `noImplicitAny` is enabled — no `any` unless unavoidable.
- **TanStack Query** for all server state. Add query keys to `src/lib/queryKeys.ts`.
- **shadcn/ui** + **Tailwind CSS** for all UI; variants via `cva`. Minimal inline styles.
- One component per file. No side effects in `render`.
- Forms use `react-hook-form` + `@hookform/resolvers/zod`. No ad-hoc uncontrolled state.
- Validate request/response shapes with **zod**.
- Accessibility: all interactive elements must be keyboard-reachable with correct `aria-*` attributes.
- Format before committing: `pnpm --filter client-vite format:write`

---

## Running Tests

### Backend (XUnit integration tests)

```bash
# From repo root
ASPNETCORE_ENVIRONMENT=Testing dotnet test ./api/api.sln -c Release

# Watch mode
ASPNETCORE_ENVIRONMENT=Testing dotnet test ./api/api.sln --watch
```

Tests use `TestingWebAppFactory` which spins up a per-test in-memory SQLite database and the `Seed.SeedAsync` helper for consistent fixture data.

### Frontend (Vitest)

```bash
cd client-vite

npm test -- --run          # single run
npm test                   # watch mode
npm run typecheck          # TypeScript only
npm run lint:strict        # ESLint zero-warnings
npm run format             # Prettier check
```

---

## Database Migrations

**Every model change requires a named migration.**

```bash
# From the api directory
cd api

# Add migration
dotnet ef migrations add <DescriptiveName> --project api.csproj

# Verify migration (review the generated file)
# Apply locally
dotnet ef database update
```

Include the generated `Migrations/<timestamp>_<Name>.cs` and updated `Migrations/AppDbContextModelSnapshot.cs` in your PR.

Scripts in `api/scripts/` (PowerShell and Bash) wrap the above commands for convenience.

---

## Adding a New Game Importer

1. Create `api/Importing/<GameName>Importer.cs` implementing `ICardImporter`.
2. Register the importer in `api/Program.cs` (DI registration block).
3. Add dry-run support — the importer must handle `dryRun: true` without persisting data.
4. Use checksums/upserts so the import is **idempotent** (safe to re-run).
5. Document the expected file format in `ADMIN.md`.
6. Add at least one integration test in `api.Tests/Features/Admin/`.

---

## Submitting Changes

1. **Fork and branch** from `dev`:
   ```bash
   git checkout dev
   git pull origin dev
   git checkout -b feature/<short-description>
   ```

2. **Make your changes** following the coding standards above.

3. **Run all checks locally** before pushing:
   ```bash
   dotnet format ./api/api.sln --verify-no-changes
   ASPNETCORE_ENVIRONMENT=Testing dotnet test ./api/api.sln -c Release
   cd client-vite && npm run lint:strict && npm run typecheck && npm run format && npm test -- --run
   ```

4. **Open a PR** against `dev` (not `main`). Use the PR template and fill out every section.

5. **Link any related issues** with `Closes #N` in the PR description.

---

## Branch Strategy

| Branch | Purpose |
|--------|---------|
| `main` | Stable, production-ready code |
| `dev` | Integration branch for features/fixes |
| `feature/<name>` | New feature development |
| `fix/<name>` | Bug fixes |
| `chore/<name>` | Maintenance, dependency updates, tooling |

PRs from feature/fix/chore branches → `dev` → eventually merged to `main` for releases.

---

## Questions?

Open a [GitHub Discussion](https://github.com/Xercius/Trading-Card-Game-Tracker/discussions) or file an issue using the appropriate template.
