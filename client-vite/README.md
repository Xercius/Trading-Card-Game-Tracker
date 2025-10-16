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
VITE_API_BASE=/api
VITE_DEV_SERVER_PROXY_TARGET=https://localhost:7226
```

When `VITE_API_BASE` is relative (starts with `/`), the Vite proxy remains active and forwards requests to `VITE_DEV_SERVER_PROXY_TARGET`. If not specified, the proxy target defaults to `https://localhost:7226`.

**Note:** When using HTTPS targets (like `https://localhost:7226`), you may need to trust the self-signed certificate. The Vite proxy is configured with `secure: false` to bypass certificate validation during development.

### Quick start

Copy `.env.example` to `.env.local` and adjust as needed:

```bash
cp .env.example .env.local
```

### Environment variables reference

- `VITE_API_BASE` (**preferred**) – API base URL. Can be absolute (`https://localhost:7226/api`) or relative (`/api`).
- `VITE_API_BASE_URL` (**deprecated**) – legacy name still recognized for compatibility.
- `VITE_DEV_SERVER_PROXY_TARGET` (optional) – HTTPS target for the Vite proxy when `VITE_API_BASE` is relative. Defaults to `https://localhost:7226`.

**Precedence:** When both `VITE_API_BASE` and the legacy `VITE_API_BASE_URL` are defined, `VITE_API_BASE` takes precedence. Prefer defining only `VITE_API_BASE`.

During development, the client logs warnings when:

- neither `VITE_API_BASE` nor `VITE_API_BASE_URL` is defined (falls back to `/api`), or
- only the deprecated `VITE_API_BASE_URL` is found.

## Development

Install dependencies and start the dev server:

```bash
npm install
npm run dev
```

The app expects the API to be available at the configured base URL. Requests are issued through a shared Axios instance that automatically attaches a bearer token obtained when the user is selected.

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
