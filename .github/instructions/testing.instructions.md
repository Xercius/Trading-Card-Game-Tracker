---
applyTo: "api.Tests/**,client-vite/src/**/__tests__/**,client-vite/src/**/*.test.{ts,tsx}"
---

# Testing Guidelines

## Running Tests

```bash
# Backend (xUnit)
ASPNETCORE_ENVIRONMENT=Testing dotnet test ./api/api.sln -c Release

# Frontend (Vitest)
cd client-vite && npm test -- --run

# Single backend test file
ASPNETCORE_ENVIRONMENT=Testing dotnet test ./api/api.sln -c Release --filter "FullyQualifiedName~<TestClassName>"
```

## Backend Testing (xUnit)

### Test Structure
- One test class per feature area, e.g., `CardsControllerTests`, `DecksServiceTests`.
- Test classes live in `api.Tests/Features/<Area>/`.
- Use `WebApplicationFactory<Program>` for integration tests against real HTTP endpoints.
- Each test gets its own SQLite database (in-memory or temp file) to ensure isolation.

### Integration Test Pattern
```csharp
// One test = one HTTP call + one set of assertions
[Fact]
public async Task GetCard_ReturnsCard_WhenExists()
{
    // Arrange: seed minimal fixture data
    // Act: call the HTTP endpoint via the test client
    // Assert: check HTTP status code + JSON shape
}
```

- Assert HTTP status codes explicitly (200, 201, 204, 400, 404, 409).
- Assert JSON response shape — at minimum, check that expected fields are present and correct.
- Do not assert implementation details (SQL queries, internal service calls).

### EF Core Tests
- Wrap EF-level tests in a transaction and roll back after each test to keep the DB clean.
- Seed with minimal, deterministic fixtures (fixed IDs, stable names).
- Use the `Testing` environment (`ASPNETCORE_ENVIRONMENT=Testing`) to activate the test DB and any test-specific configuration.

### Time & Clock
- Inject a fixed-clock shim via `ISystemClock` (or an equivalent test double) to make time-dependent tests deterministic.
- Never use `DateTime.Now` or `DateTimeOffset.UtcNow` directly in code under test — always inject the clock.

### Mocking Guidelines
- Prefer real implementations over mocks for EF Core and business logic layers — use the actual DB.
- Mock external HTTP clients (Scryfall, Pokémon TCG API, etc.) using `HttpMessageHandler` test doubles or `MockHttpMessageHandler`.
- Mock `ICurrentUser` for controller tests that require an authenticated user.
- Use `Moq` or `NSubstitute` for service-layer unit tests when isolating a single class.

### Naming Convention
- Test method names: `<MethodOrEndpoint>_<Scenario>_<ExpectedOutcome>`, e.g., `GetCard_WhenNotFound_Returns404`.
- Keep test names readable as documentation — they should describe business behavior, not implementation.

### What to Test
- Happy path for every public endpoint.
- Validation failures (400) for invalid inputs.
- Not-found cases (404) for missing resources.
- Conflict cases (409) for uniqueness violations.
- Admin-guard enforcement (403) for protected endpoints.
- Import idempotency — running an import twice must not create duplicate rows.

## Frontend Testing (Vitest + React Testing Library)

### Test File Location
- Place test files in a `__tests__/` subdirectory next to the component or hook being tested.
- Name test files `<ComponentName>.test.tsx` or `<hookName>.test.ts`.

### Component Test Pattern
```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ComponentUnderTest } from '../ComponentUnderTest';

it('shows error message when input is invalid', async () => {
  render(<ComponentUnderTest />);
  await userEvent.type(screen.getByRole('textbox', { name: /name/i }), '');
  await userEvent.click(screen.getByRole('button', { name: /submit/i }));
  expect(screen.getByText(/name is required/i)).toBeInTheDocument();
});
```

### Query Priority (React Testing Library)
Always query elements in this priority order:
1. `getByRole` (most preferred — mirrors how assistive tech works)
2. `getByLabelText`
3. `getByPlaceholderText`
4. `getByText`
5. `getByDisplayValue`
6. `getByAltText`, `getByTitle` (least preferred)

Never use `getByTestId` unless absolutely necessary.

### Mocking API Calls
- Mock TanStack Query responses by wrapping components in a `QueryClientProvider` with a test `QueryClient`.
- Mock `fetch`/Axios calls using `vi.mock` or `msw` (Mock Service Worker) for realistic network simulation.
- Prefer `msw` handlers for integration-style component tests that exercise the full data-fetching path.

### Hook Testing
- Use `renderHook` from `@testing-library/react` to test custom hooks in isolation.
- Provide required context (QueryClient, UserContext) via wrappers.

### What to Test
- Component renders the correct UI for given props/state.
- User interactions (click, type, submit) produce expected side effects.
- Error states render appropriate fallback UI or messages.
- Loading states show spinners or skeletons while data is pending.
- Accessibility: confirm key elements have correct roles and labels.

### What NOT to Test
- Internal implementation details (state variable names, specific function calls).
- Styling/visual appearance (use snapshot tests sparingly, only for stable UI).
- Third-party library behavior (shadcn/ui, React Query internals).

## Test Quality Standards

- **Deterministic:** Tests must produce the same result on every run. No random data, no time-dependent logic without clock injection.
- **Isolated:** Each test is independent. No shared mutable state between tests.
- **Fast:** Unit and component tests should complete in milliseconds. Slow tests suggest over-testing implementation details.
- **Readable:** A failing test should clearly communicate what behavior broke.
- **No test pollution:** Clean up after each test (rollback transactions, reset mocks, unmount components).
