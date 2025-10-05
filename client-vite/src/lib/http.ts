import axios, {
  AxiosHeaders,
  type InternalAxiosRequestConfig,
} from "axios";

// ------------------------------------
// Base URL setup
// ------------------------------------
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
const baseURL = String(rawBase).replace(/\/+$/, "") + "/";

const http = axios.create({ baseURL });

const basePath: string | null = (() => {
  try {
    const parsed = new URL(baseURL, "http://axios-base.local");
    const pathname = parsed.pathname || "/";
    const normalized = pathname.replace(/\/+$/, "");
    if (!normalized) {
      return "/";
    }
    return `${normalized}/`;
  } catch (error) {
    if (import.meta.env.DEV) {
      // eslint-disable-next-line no-console
      console.warn("[http] Failed to derive base path from baseURL", error);
    }
    return null;
  }
})();

// ------------------------------------
// X-User-Id management
// ------------------------------------
let currentUserId: number | null = null;

type MutableCommonHeaders = Record<string, string>;

export function setHttpUserId(id: number | null) {
  currentUserId = id ?? null;

  // Update defaults for any non-interceptor calls
  const common = http.defaults.headers.common as unknown as MutableCommonHeaders;
  if (currentUserId == null) {
    delete common["X-User-Id"];
  } else {
    common["X-User-Id"] = String(currentUserId);
  }
}

const __initialUserId = Number(localStorage.getItem("userId") ?? 1) || 1;
setHttpUserId(__initialUserId);

// ------------------------------------
// Dev absolute-path warning allowlist
// ------------------------------------
const warnedOnce = new Set<string>();
const ABSOLUTE_OK_PREFIXES: string[] = (import.meta.env.VITE_HTTP_ABS_OK ?? "")
  .split(",")
  .map((s: string) => s.trim())
  .filter(Boolean);

function isAbsoluteHttpUrl(u: string): boolean {
  return /^https?:\/\//i.test(u);
}

type Cfg = InternalAxiosRequestConfig & {
  suppressBaseURLWarning?: boolean;
};

// ------------------------------------
// Request Interceptor
// ------------------------------------
http.interceptors.request.use((cfg: Cfg) => {
  if (import.meta.env.DEV && typeof cfg.url === "string") {
    const url = cfg.url;
    const suppress = cfg.suppressBaseURLWarning === true;
    const isAbsSameOriginPath = url.startsWith("/") && !isAbsoluteHttpUrl(url);
    const isAllowed = ABSOLUTE_OK_PREFIXES.some((prefix) => url.startsWith(prefix));

    if (isAbsSameOriginPath && !suppress && !isAllowed) {
      const base = http.defaults.baseURL ?? "";
      const key = `${base} -> ${url}`;
      if (!warnedOnce.has(key)) {
        // eslint-disable-next-line no-console
        console.warn(
          `[http] Absolute same-origin path "${url}" bypasses axios baseURL "${base}". ` +
            'Use "cards" instead of "/cards", ' +
            "or pass { suppressBaseURLWarning: true }, " +
            `or allow via VITE_HTTP_ABS_OK="${ABSOLUTE_OK_PREFIXES.join(",")}".`
        );
        warnedOnce.add(key);
      }
    }
  }

  if (typeof cfg.url === "string" && basePath && basePath !== "/") {
    const url = cfg.url;
    const isAbsSameOriginPath = url.startsWith("/") && !isAbsoluteHttpUrl(url);
    if (isAbsSameOriginPath && url.startsWith(basePath)) {
      cfg.url = url.slice(basePath.length);
    }
  }

  // Normalize headers with from() to avoid ctor type mismatch
  const headers = AxiosHeaders.from(cfg.headers);

  if (currentUserId != null) {
    headers.set("X-User-Id", String(currentUserId));
  } else {
    headers.delete("X-User-Id");
  }

  cfg.headers = headers;
  return cfg;
});

// ------------------------------------
// Exports
// ------------------------------------
export default http;

export function __debugGetCurrentUserId() {
  return import.meta.env.DEV ? currentUserId : null;
}
