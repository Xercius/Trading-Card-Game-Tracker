---
applyTo: "client-vite/src/**/*.{ts,tsx}"
---

# React / TypeScript Guidelines

## Architecture

### New React Page / Feature Checklist
1. Create page component in `client-vite/src/pages/`.
2. Add route in `client-vite/src/routes/index.tsx` with `Suspense` + `ErrorBoundary`.
3. Add API function in `client-vite/src/features/api/`.
4. Register query key in `client-vite/src/lib/queryKeys.ts`.
5. Use `useQuery`/`useMutation` from TanStack Query in the component.
6. Add Vitest test in a `__tests__/` subdirectory next to the component.

### Module Structure
- Feature code lives in `client-vite/src/features/<area>/`.
- Shared UI primitives go in `components/ui/` (shadcn/ui based).
- Reusable non-UI hooks go in `hooks/useFeatureName.ts`.
- API client functions are grouped by resource in `features/api/`.
- Query keys are centralized in `lib/queryKeys.ts`.

## Coding Standards

### General TypeScript
- Functional components and hooks only. No class components.
- Strict typing: `noImplicitAny` is enforced. Annotate all function parameters and return types.
- One component per file. No side effects at module level or in the render body.
- Use `zod` for request/response schema validation. Generate types from schemas where possible.
- Use `react-hook-form` + `@hookform/resolvers/zod` for all forms. No ad-hoc uncontrolled state.
- Imports ordered: framework → third-party packages → app/feature modules → relative.

### File Size & Component Modularity
- **Page components:** Keep under 250 lines. Extract sections into named sub-components when exceeded.
- **Presentational components:** Aim for under 150 lines. If a component renders multiple independent sections, extract each into its own file.
- **Custom hooks:** Extract stateful or side-effect-heavy logic into `useFeatureName` hooks in `hooks/`. Keep components primarily declarative.
- **Sub-components:** Place reusable sub-components in separate files; compose them rather than expanding a monolith.
- **Import coupling:** If a file needs imports from 10+ modules, treat it as a coupling warning and refactor.

### Method & Complexity Limits
- Keep component render logic under 40 lines per function. Extract helpers for anything longer.
- Maximum nesting depth: 3 levels. Use early returns and guard clauses.
- Avoid complex conditional JSX trees — break them into named sub-components or helper render functions.

## State Management

### Server State (TanStack Query)
- All server-fetched data lives in React Query. Never duplicate in local state.
- Centralize query keys in `lib/queryKeys.ts`, e.g. `['cards', { game, set, page, q }]`.
- Configure `staleTime` per query based on data volatility.
- Use `select` option to shape/transform data from the query cache.
- Implement optimistic updates with `onMutate` / `onError` / `onSettled` for mutations.

### Local UI State
- Local UI state (modals, toggles, form focus) stays in components with `useState`/`useReducer`.
- Avoid global state for anything that can be derived from server state or scoped locally.
- Use `UserContext` (`state/UserContext.tsx`) only for cross-route auth state (current user, token).

## Routing & Code Splitting
- Define per-feature route modules. Wrap each lazy-loaded route with `Suspense` and `ErrorBoundary`.
- Route definitions live in `client-vite/src/routes/index.tsx`.
- Keep route-level components thin — they should compose feature components, not contain logic.

## Data Fetching & API Layer
- API functions live in `features/api/` grouped by resource (e.g., `cardsApi.ts`, `decksApi.ts`).
- Use the Axios instance from `lib/http.ts` — it handles auth header injection automatically.
- All API functions return typed responses using zod schemas or explicit TypeScript interfaces.

## Styling & UI

### shadcn/ui
- Use the local registry for shadcn/ui components. Do not import from `@shadcn/ui` directly.
- Build variants with `cva`. Minimize inline styles.
- Prefer Tailwind utility classes over custom CSS.

### Accessibility
- All interactive elements must be keyboard reachable.
- Add proper `aria-*` attributes on custom controls (dropdowns, dialogs, toggles).
- Use semantic HTML elements (`button`, `nav`, `main`, `section`) before adding ARIA roles.

### Performance
- Use `@tanstack/react-virtual` for card grids and any list with more than ~50 items.
- Memoize expensive derived values with `useMemo`. Memoize callbacks passed as props with `useCallback`.
- Avoid re-renders caused by object/array literals in JSX props — extract to constants or `useMemo`.

## Error Handling

- Wrap route-level components with `ErrorBoundary` to catch render errors and show a fallback UI.
- Handle `isError` from `useQuery`/`useMutation` — display user-friendly error messages, not raw error objects.
- Use `toast` notifications for mutation success/failure feedback.
- Never expose raw API error messages or stack traces to the user.

## Documentation Standards (JSDoc)
- Add JSDoc comments to exported custom hooks, API functions, and utility functions.
- Document non-obvious props with `/** */` comments in the component's prop type definition.
- Keep comments concise — explain *why*, not *what* (the code already shows what).

## Anti-patterns to Avoid

- **Monolithic components:** Single files doing data fetching, form state, rendering, and side effects.
- **Prop drilling beyond 2 levels:** Use context or query keys to share state.
- **useEffect for derived state:** Compute it inline or with `useMemo` instead.
- **Direct DOM manipulation:** Always use React state and refs; never `document.getElementById`.
- **Untyped API responses:** Always define or infer types for all API data.
