---
applyTo: "api/Features/**,api/Controllers/**,api/Infrastructure/**,api/Common/**,api/Shared/**"
---

# C# API

- Structure features as `Features/<Area>/{Controller,Dtos,Mapping,Services,Validation}`. A feature must not reference another feature's service; put cross-feature infrastructure (base classes, extensions, result types) in `api/Common` and cross-feature domain services and DTOs in `api/Shared`.
- New endpoint: controller action → `record` DTOs → FluentValidation validator → AutoMapper profile → service (DI) → integration test in `api.Tests/`.
- Map entity↔DTO only in AutoMapper profiles.
- Actions return `ActionResult<T>`; errors via `ProblemDetails`; `POST` → 201 + `Location`; validation 400 (`errors` extension), 403, 404, 409.
- Pass `CancellationToken` from actions to EF/HttpClient. No `.Result`/`.Wait()`. No raw SQL where LINQ works.
- Timestamps: `DateTimeOffset`/UTC only; obtain time via injected `TimeProvider`.
- Config: `IOptions<T>` + `ValidateOnStart()`.
- New list endpoints: `page` (default 1), `pageSize` (default 20, max 100), `X-Total-Count` header; return 400 (validation error) when `page<1`, `pageSize<1`, or `pageSize>100`; routes `/api/<plural-noun>`, no verbs in paths.
- EF reads: `.AsNoTracking()`; prefer `Select` projections; `.AsSplitQuery()` when loading multiple sibling collections; `BeginTransactionAsync()` for multi-entity writes.
- Case-insensitive search: `EF.Functions.Like` or NOCASE collation, never `ToLower()`.
- Logging: structured, `EventId` constants per feature, never log tokens/passwords/PII.
- Limits: service ≤300 lines, controller ≤200, method ≤50, nesting ≤3 (use guard clauses).
