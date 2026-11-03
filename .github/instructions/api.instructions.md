---
applyTo: "api/Features/**,api/Controllers/**,api/Importing/**,api/Infrastructure/**,api/Common/**,api/Shared/**"
---

# C# API

- Structure features as `Features/<Area>/{Controller,Dtos,Mapping,Services,Validation}`. A feature must not reference another feature's service; put shared code in `api/Common` or `api/Shared`.
- New endpoint: controller action → `record` DTOs → FluentValidation validator → AutoMapper profile → service (DI) → integration test in `api.Tests/`.
- Map entity↔DTO only in AutoMapper profiles.
- Actions return `ActionResult<T>`; errors via `ProblemDetails`; `POST` → 201 + `Location`; validation 400 (`errors` extension), 403, 404, 409.
- Pass `CancellationToken` from actions to EF/HttpClient. No `.Result`/`.Wait()`. No raw SQL where LINQ works.
- Timestamps: `DateTimeOffset`/UTC only; obtain time via injected `TimeProvider`.
- Config: `IOptions<T>` + `ValidateOnStart()`.
- New list endpoints: `page`, `pageSize` (max 100), `X-Total-Count` header; routes `/api/<plural-noun>`, no verbs in paths.
- EF reads: `.AsNoTracking()`; prefer `Select` projections; `.AsSplitQuery()` when loading multiple sibling collections; `BeginTransactionAsync()` for multi-entity writes.
- Case-insensitive search: `EF.Functions.Like` or NOCASE collation, never `ToLower()`.
- Logging: structured, `EventId` constants per feature, never log tokens/passwords/PII.
- Limits: service ≤300 lines, controller ≤200, method ≤50, nesting ≤3 (use guard clauses).
