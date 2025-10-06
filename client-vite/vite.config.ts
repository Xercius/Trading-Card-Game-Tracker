import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react-swc";
import { fileURLToPath, URL } from "node:url";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  const apiBase = env.VITE_API_BASE || env.VITE_API_BASE_URL || "";
  const shouldProxy = !apiBase || apiBase.startsWith("/");
  const proxyTarget = env.VITE_DEV_SERVER_PROXY_TARGET || "https://localhost:7226";

  return {
    plugins: [react()],
    resolve: {
      alias: {
        "@": fileURLToPath(new URL("./src", import.meta.url)),
      },
    },
    server: shouldProxy
      ? {
          proxy: {
            "/api": {
              target: proxyTarget,
              changeOrigin: true,
              secure: false,
            },
          },
        }
      : undefined,
    test: {
      environment: "jsdom",
    },
  };
});
