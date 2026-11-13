# Trading Card Game Tracker – Client

This Vite-powered React app provides the front-end for the Trading Card Game Tracker. It uses React Router, TanStack Query, Tailwind, and shadcn/ui components.

## Environment configuration

The API base URL is configured through environment variables. There are two supported configurations:

### Configuration 1: Direct connection (Recommended)

Use an absolute URL to connect directly to the API server without the Vite dev server proxy. This is the simplest and most reliable approach.

**When to use:**

- Standard local development
- When you want to avoid proxy overhead
- When the API server is readily accessible

**Setup:**

Create `.env.local` in the `client-vite` directory:

```bash
# client-vite/.env.local
VITE_API_BASE=https://localhost:7226/api
```

With an absolute base URL, Axios connects directly to the API and the Vite proxy automatically disables itself.

### Configuration 2: Proxy-based connection

Use a relative URL with the Vite dev server proxy to forward requests to the API server. This is useful for advanced scenarios.

**When to use:**

- You need to route through the Vite dev server
- You want a different API server than the default
- You're testing proxy behavior

**Setup:**

Create `.env.local` in the `client-vite` directory:

```bash
# client-vite/.env.local

# HTTPS
VITE_API_BASE=/api
VITE_DEV_SERVER_PROXY_TARGET=https://localhost:7226

# HTTP
# VITE_API_BASE=/api
# VITE_DEV_SERVER_PROXY_TARGET=http://localhost:5229
```

When `VITE_API_BASE` is relative (starts with `/`), the Vite proxy remains active and forwards requests to `VITE_DEV_SERVER_PROXY_TARGET`. If not specified, the proxy target defaults to `https://localhost:7226`.

**Note:** The proxy supports both HTTP and HTTPS target origins. When using HTTPS targets (like `https://localhost:7226`), you may need to trust the self-signed certificate. The Vite proxy is configured with `secure: false` to bypass certificate validation during development.

### Quick start

Copy `.env.example` to `.env.local` and adjust as needed:

```bash
cp .env.example .env.local
```

### Environment variables reference

- `VITE_API_BASE` (**preferred**) – API base URL. Can be absolute (`https://localhost:7226/api`) or relative (`/api`). Defaults to `/api`.
- `VITE_DEV_SERVER_PROXY_TARGET` (optional) – Proxy target origin for the Vite proxy when `VITE_API_BASE` is relative. Defaults to `https://localhost:7226`.
- `VITE_HTTP_ABS_OK` (optional) – Comma-separated allowlist of same-origin absolute request paths permitted without triggering dev baseURL warnings in `src/lib/http.ts`.

## Development

Install dependencies and start the dev server:

```bash
npm install
npm run dev
```

Available scripts:

- `npm run dev` – start Vite dev server
- `npm run lint:strict` – run ESLint with `--max-warnings=0`
- `npm run typecheck` – run TypeScript (`tsc -b`)
- `npm run format` / `npm run format:write` – check or fix Prettier formatting
- `npm test` / `npm run test:coverage` – run Vitest unit/component tests
- `npm run build` – typecheck and build production bundle
- `npm run storybook` / `npm run build-storybook` – run or build Storybook

Implemented routes (`src/routes/paths.ts`): `/cards`, `/collection`, `/collection/set/:setCode`, `/wishlist`, `/decks`, `/decks/:deckId`, `/value`, `/users`, `/admin/import`, `/admin/sync`, `/admin/swccg-sync`, and `/admin/pokemon-sync`.

The app expects the API to be available at the configured base URL. Requests are issued through the shared Axios instance (`src/lib/http.ts`) in single-user mode, while admin sync endpoints use an HttpOnly `AdminSession` cookie and `X-Admin-CSRF` header.

### Verifying API connectivity

Before starting the client, ensure the API server is running and reachable. Test the health endpoint:

```bash
curl -k https://localhost:7226/api/health
```

Expected response: `{"status":"healthy","timestamp":"..."}`

If you see ECONNREFUSED errors, ensure:

1. The API server is running on the correct port (default: https://localhost:7226)
2. `VITE_API_BASE` in `.env.local` matches the API server URL
3. If using a self-signed certificate, the `-k` flag bypasses certificate validation
