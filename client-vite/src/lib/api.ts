import axios from "axios";

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? "/api",
});

let currentUserId: number =
  typeof window !== "undefined" && typeof localStorage !== "undefined"
    ? Number(localStorage.getItem("apiUserId") ?? "1") || 1
    : 1;

export function setApiUserId(id: number) {
  currentUserId = id;
  // keep defaults aligned for any direct axios usage
  api.defaults.headers.common["X-User-Id"] = String(id);
}

api.interceptors.request.use((config) => {
  // always stamp current user
  config.headers = config.headers ?? {};
  (config.headers as any)["X-User-Id"] = String(currentUserId);
  return config;
});
