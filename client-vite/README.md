# Trading Card Game Tracker – Client

This Vite-powered React app provides the front-end for the Trading Card Game Tracker. It uses React Router, TanStack Query, Tailwind, and shadcn/ui components.

## Environment configuration

The API base URL is configured through environment variables:

- `VITE_API_BASE` (**preferred**) – points at the root of the API server (for example `https://api.example.com` or `/api`).
- `VITE_API_BASE_URL` (**deprecated**) – legacy name still recognized for compatibility.
- `VITE_DEV_SERVER_PROXY_TARGET` (optional) – overrides the HTTPS target used by the Vite dev server proxy when `VITE_API_BASE` is relative.

Only one value is required. When both are present, `VITE_API_BASE` wins. During development the client logs warnings when:

- neither variable is defined (falls back to `/api`), or
- only the deprecated `VITE_API_BASE_URL` is found.

Best practice is to point `VITE_API_BASE` at the actual backend origin in every environment (for example `https://localhost:7226/api` locally). When the base is absolute, Axios connects to the API directly and the Vite proxy automatically disables itself. If you intentionally use a relative base such as `/api`, the proxy remains active and forwards requests to `VITE_DEV_SERVER_PROXY_TARGET` (defaulting to `https://localhost:7226`).

Copy `.env.example` to `.env.local` (or `.env`) and adjust the values as needed.

```bash
cp .env.example .env.local
```

## Development

Install dependencies and start the dev server:

```bash
npm install
npm run dev
```

The app expects the API to be available at the configured base URL. Requests are issued through a shared Axios instance that automatically adds the `X-User-Id` header based on the currently selected user.
