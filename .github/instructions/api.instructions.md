---
applyTo: "api/**/*.cs"
---

# C# API Guidelines

## Architecture

### Feature Folders
- Structure: `/Features/<Area>/{Controller, Dtos, Mapping, Services, Validation}`.
- No cross-feature coupling. If two features share logic, extract it to `Features/_Common/`.
- Each feature owns its own service, DTOs, mapping profile, and validator.

### New API Endpoint Checklist
1. Add controller method in `Features/<Area>/<Area>Controller.cs`.
2. Create request/response DTOs as `record` types in `Features/<Area>/Dtos/`.
3. Add FluentValidation validator in `Features/<Area>/Validation/`.
4. Add AutoMapper profile in `Features/<Area>/Mapping/`.
5. Wire service logic in `Features/<Area>/Services/` (inject via DI).
6. Add integration test in `api.Tests/Features/<Area>/`.

## Coding Standards

### General C#
- Prefer async/await throughout. Inject all dependencies via DI; avoid `new` for services.
- Use `record` types for immutable DTOs. Avoid mutable classes for data transfer.
- Controllers return `ActionResult<T>` and RFC 7807 `ProblemDetails` for errors.
- Case-insensitive search: avoid `ToLower()`. Use SQLite NOCASE collation or `EF.Functions.Like` with normalized columns.
- Map entities ↔ DTOs only via AutoMapper profiles — never manually in controllers or services.
- Pass `CancellationToken` from controller actions down to EF Core and `HttpClient` calls.
- UTC-only timestamps: persist with `DateTimeOffset` or UTC `DateTime`. Never use local time.
- Use `IOptions<T>` + `ValidateOnStart()` for all configuration sections.

### File Size & Modularity
- **Services/handlers:** Keep under 300 lines. If a service spans multiple domains (e.g., pricing vs. valuation), split by responsibility.
- **Controllers:** Keep under 200 lines. Move business logic to services; split by resource area when exceeded.
- **Helper methods:** Group related private helpers near the consuming method. Extract shared cross-service logic into focused helper classes rather than growing one service.
- **Imports/usings:** Group as: framework → third-party packages → app/feature modules → relative. Flag files needing 10+ usings for coupling review.

### Guard Clauses & Flow
- Use guard clauses and early returns instead of deeply nested `if` blocks.
- Maximum nesting depth: 3 levels. Extract inner logic into named helpers beyond that.
- Keep methods under 50 lines. Extract helpers for anything longer.
- Cyclomatic complexity: avoid more than 10 branch points per method.

## EF Core Patterns

- **Read vs. write:** Reads use `.AsNoTracking()`. Writes track entities normally.
- **Paging:** All list endpoints accept `page` and `pageSize` (max cap at 100). Return `X-Total-Count` header.
- **Search:** Use `EF.Functions.Like(col, pattern)` or NOCASE collation. Never `ToLower()` — it prevents index use.
- **Projection:** Prefer `Select` projections into DTOs over `Include` chains. Use `.AsSplitQuery()` for large multi-collection includes.
- **Indexes:** Add indexes on foreign keys, lookup columns, and normalized text columns.
- **Transactions:** Use `BeginTransactionAsync()` for multi-entity writes.
- **Concurrency:** Use `RowVersion` or concurrency tokens on mutable aggregates.
- **Migrations:** Every model change includes a named migration + snapshot diff. Migration name should describe the change (e.g., `AddCardPrintingSetIndex`).

## Validation & Error Handling

### FluentValidation
- Create a per-request validator in `Features/<Area>/Validation/<RequestName>Validator.cs`.
- Register validators via DI (auto-scanned). Return HTTP 400 with validation errors in `errors` extension per RFC 7807.

### Error Responses
- Use a central `ProblemDetailsFactory` to map known exception types → 400/404/409 with consistent `type`, `instance`, and `title`.
- HTTP status conventions: 200 (OK), 201 (Created), 204 (No Content), 400 (Validation), 404 (Not Found), 409 (Conflict), 403 (Forbidden).
- `POST` returns `201 Created` with a `Location` header pointing to the created resource.
- Never return raw exception messages or stack traces to the client.

### Logging
- Use structured logs with `EventId` constants. Define event IDs per feature area.
- Use `BeginScope` for request/deck/user context to correlate log entries.
- Log at `Warning` for business-rule violations (not found, conflict), `Error` for unexpected exceptions.
- Never log sensitive data (tokens, passwords, PII).

## HTTP API Conventions

- **Routes:** `/api/<plural-resource>` with REST verbs. No verbs in paths (use HTTP method instead).
- **Idempotency:** `POST /api/admin/import` uses checksums/upserts. Re-running an import never creates duplicates.
- **ETags (optional):** Heavy GETs may include ETags. Honor `If-None-Match` to return 304.
- **Pagination:** Return `X-Total-Count` header with all paged list responses.

## Security Patterns

- **Current user:** Use `ICurrentUser` abstraction from claims. Controllers must not parse request headers directly.
- **Admin guards:** `[RequireAdmin]` attribute returns `403 ProblemDetails`. Guard last-admin operations (demote/delete) with `409 Conflict`.
- **Forwarded headers:** Resolve client IP from rightmost non-trusted-proxy entry in `X-Forwarded-For`, within the configured `ForwardLimit`.
- **Secrets:** Load via environment variables or `appsettings.*.json` files that are `.gitignore`d. Never hardcode secrets.

## Anti-patterns to Avoid

- **God services:** One service owning unrelated workflows, caching, validation, AND orchestration.
- **Fat controllers:** Business logic in controllers instead of services.
- **Raw SQL strings** when EF Core can express the query safely.
- **Synchronous blocking** (`Result`, `Wait()`) on async methods.
- **Cross-feature direct dependencies:** Feature A's service should not import Feature B's service directly; use shared `_Common` abstractions.
