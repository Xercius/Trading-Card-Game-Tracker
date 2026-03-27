## Summary

<!-- Briefly describe what this PR does and why. Link any related issues with "Closes #N" or "Fixes #N". -->

## Changes

<!-- List the key changes made. -->

- 

## Testing

<!-- Describe the commands you ran and the expected output. -->

```bash
# Backend
dotnet test ./api/api.sln -c Release

# Frontend
npm test -- --run
```

## Checklist

### General
- [ ] PR targets the correct branch (`dev` for features/fixes, `master` for releases)
- [ ] One concern per PR — unrelated changes are split into separate PRs
- [ ] Commit messages are clear and descriptive

### Code Quality
- [ ] `dotnet format ./api/api.sln --verify-no-changes` passes (or `dotnet format` was run locally)
- [ ] `pnpm lint:strict` passes with zero warnings
- [ ] `pnpm typecheck` passes
- [ ] `pnpm format` (Prettier) was run on all modified TS/TSX/JSON files

### Testing
- [ ] All existing tests pass (`dotnet test` + `npm test -- --run`)
- [ ] New feature/fix is covered by at least one test
- [ ] No tests were removed or disabled without justification

### Database (if applicable)
- [ ] A named EF Core migration was added (`dotnet ef migrations add <Name>`)
- [ ] Migration snapshot diff is included in the PR

### API / Routes (if applicable)
- [ ] New routes follow `/api/<plural>` REST conventions (no verbs in paths)
- [ ] `POST` endpoints return `201 Created` with a `Location` header
- [ ] Error responses use RFC 7807 `ProblemDetails`
- [ ] `CancellationToken` is threaded through to EF queries

### Frontend (if applicable)
- [ ] New components are in feature folders or `components/`; one component per file
- [ ] Server state managed via TanStack Query; local UI state in component
- [ ] New query keys added to `client-vite/src/lib/queryKeys.ts`
- [ ] Accessibility: keyboard reachable, proper `aria-*` attributes

### Security
- [ ] No secrets or credentials committed
- [ ] No new dependencies added without reviewing the advisory database
- [ ] Input is validated (FluentValidation on the API; zod on the client)
