import axios from "axios";

const envBase =
  (import.meta.env.VITE_API_BASE as string | undefined) ??
  (import.meta.env.VITE_API_BASE_URL as string | undefined);

if (import.meta.env.DEV) {
  if (!envBase) {
    // eslint-disable-next-line no-console
    console.warn("[http] No VITE_API_BASE or VITE_API_BASE_URL set; defaulting to '/api'.");
  } else if (!import.meta.env.VITE_API_BASE && import.meta.env.VITE_API_BASE_URL) {
    // eslint-disable-next-line no-console
    console.warn("[http] VITE_API_BASE_URL is deprecated; please migrate to VITE_API_BASE.");
  }
}

const rawBase = envBase ?? "/api";
// strip trailing slashes so join is predictable
const baseURL = String(rawBase).replace(/\/+$/, "") + "/";

const http = axios.create({ baseURL });

// In-memory source of truth for the header
let currentUserId: number | null = null;

export function setHttpUserId(id: number | null) {
  currentUserId = id ?? null;

  // Keep defaults aligned for any non-interceptor calls
  if (currentUserId == null) {
    delete (http.defaults.headers.common as any)["X-User-Id"];
  } else {
    http.defaults.headers.common["X-User-Id"] = String(currentUserId);
  }
}

const warnedOnce = new Set<string>();
// Allow specific absolute prefixes via VITE_HTTP_ABS_OK="/auth,/health" (dev convenience)
const ABSOLUTE_OK_PREFIXES: string[] = (import.meta.env.VITE_HTTP_ABS_OK ?? "")
  .split(",")
  .map((s) => s.trim())
  .filter(Boolean);

function isAbsoluteHttpUrl(u: string): boolean {
  return /^https?:\/\//i.test(u);
}

http.interceptors.request.use((cfg) => {
  if (import.meta.env.DEV && typeof cfg.url === "string") {
    const url = cfg.url;
    // Pass { suppressBaseURLWarning: true } on a request config to silence intentional absolute paths
    const suppress = (cfg as any).suppressBaseURLWarning === true;
    const isAbsSameOriginPath = url.startsWith("/") && !isAbsoluteHttpUrl(url);
    const isAllowed = ABSOLUTE_OK_PREFIXES.some((prefix) => url.startsWith(prefix));

    if (isAbsSameOriginPath && !suppress && !isAllowed) {
      const base = http.defaults.baseURL ?? "";
      const key = `${base} -> ${url}`;
      if (!warnedOnce.has(key)) {
        // eslint-disable-next-line no-console
        console.warn(
          `[http] Absolute same-origin path "${url}" bypasses axios baseURL "${base}". ` +
            "Use a relative path (e.g. \"cards\") so baseURL joins correctly, " +
            "or pass { suppressBaseURLWarning: true } on this request, " +
            `or allow via VITE_HTTP_ABS_OK="${ABSOLUTE_OK_PREFIXES.join(",")}".`
        );
        warnedOnce.add(key);
      }
    }
  }

  cfg.headers = cfg.headers ?? {};
  if (currentUserId != null) {
    cfg.headers["X-User-Id"] = String(currentUserId);
  } else {
    delete (cfg.headers as any)["X-User-Id"];
  }
  return cfg;
});

export default http;
