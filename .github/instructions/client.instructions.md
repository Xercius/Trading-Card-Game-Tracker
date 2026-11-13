---
applyTo: "client-vite/src/**/*.{ts,tsx}"
---

# React / TypeScript

- New page: component in `src/pages/` (or feature panel for admin sync routes) → route in `src/routes/index.tsx` (lazy, `Suspense` + `ErrorBoundary`) → API function in `features/<area>/api.ts` (or `features/<area>/api/`) using `lib/http.ts` → key in `lib/queryKeys.ts` → Vitest test in an adjacent `__tests__/`.
- Feature code lives in `features/<area>/`; shared hooks in `hooks/`; route components only compose feature components.
- Server data lives only in TanStack Query; never copy it into `useState`. Set `staleTime` per query; optimistic updates use `onMutate/onError/onSettled`. For new queries, prefer `queryOptions()` factories so the key and fetcher are defined together and reused by `useQuery`, `prefetchQuery` and `invalidateQueries`.
- `UserContext` is for auth/current user only.
- Forms: controlled components with explicit validation; the server (FluentValidation) is authoritative and its `ProblemDetails.errors` are surfaced per field. Do not add a form or schema library without asking.
- API responses are typed with explicit interfaces mirroring the DTOs; no `any`.
- UI primitives come from `components/ui/` (local shadcn-style components, not an npm package); Tailwind classes for styling. Mutation feedback uses the existing per-page toast pattern.
- Use `@tanstack/react-virtual` for lists over 50 items. Card images: `loading="lazy"`, `decoding="async"`, and explicit `width`/`height` (or a fixed aspect-ratio box) to avoid layout shift.
- Memoization: the React Compiler is not enabled. Add `useMemo`/`useCallback`/`memo` only for a measured problem or a referential-stability requirement (e.g. a dependency of another hook), not by default.
- Security: never use `dangerouslySetInnerHTML`; render rich card text by mapping allowed tags to React elements (see `features/cards/components/CardModal.tsx`). Never store auth/admin tokens in `localStorage`/`sessionStorage` (admin auth uses the HttpOnly session cookie + antiforgery token). `localStorage` is only for non-sensitive UI preferences, wrapped in `try/catch`. `import.meta.env.VITE_*` values are public.
- Interactive elements must be keyboard-accessible, with `aria-*` on custom controls.
- Floating and overlay surfaces (dropdowns, autocomplete popovers, select menus, dialogs, drawers, and tooltips) must use an opaque background, clear border separation, and appropriate elevation.
- No `useEffect` for derived state or data fetching; no direct DOM access; no module-level side effects.
- Limits: page ≤250 lines, component ≤150, returned JSX block ≤40 lines (extract sub-components when exceeded), JSX element nesting depth ≤3.
