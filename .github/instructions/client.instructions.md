---
applyTo: "client-vite/src/**/*.{ts,tsx}"
---

# React / TypeScript

- New page: component in `src/pages/` → route in `src/routes/index.tsx` (lazy, `Suspense` + `ErrorBoundary`) → API function in `features/api/` using `lib/http.ts` → key in `lib/queryKeys.ts` → Vitest test in an adjacent `__tests__/`.
- Feature code lives in `features/<area>/`; shared hooks in `hooks/`; route components only compose feature components.
- Server data lives only in TanStack Query; never copy it into `useState`. Set `staleTime` per query; optimistic updates use `onMutate/onError/onSettled`.
- `UserContext` is for auth/current user only.
- Forms: `react-hook-form` + zod resolver. API responses are typed via zod or explicit interfaces; no `any`.
- shadcn components come from `components/ui/` (not `@shadcn/ui`); Tailwind classes, `cva` for variants.
- Use `@tanstack/react-virtual` for lists over 50 items.
- Interactive elements must be keyboard-accessible, with `aria-*` on custom controls.
- Floating and overlay surfaces (dropdowns, autocomplete popovers, select menus, dialogs, drawers, and tooltips) must use an opaque background, clear border separation, and appropriate elevation.
- No `useEffect` for derived state; no direct DOM access; no module-level side effects.
- Limits: page ≤250 lines, component ≤150, render function ≤40, nesting ≤3.
