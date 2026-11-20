import {
  copyFileSync,
  cpSync,
  existsSync,
  mkdirSync,
  readdirSync,
  rmSync,
} from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const clientDistDir = resolve(__dirname, "../client-vite/dist");
const apiWwwrootDir = resolve(__dirname, "../api/wwwroot");

if (!existsSync(resolve(clientDistDir, "index.html"))) {
  throw new Error(
    `Missing built frontend entry at "${resolve(clientDistDir, "index.html")}". Run vite build first.`,
  );
}

if (!existsSync(apiWwwrootDir)) {
  mkdirSync(apiWwwrootDir, { recursive: true });
}

// Remove only stale generated SPA outputs so locally synced assets under api/wwwroot/images remain intact.
for (const entry of ["index.html", "vite.svg", "assets"]) {
  rmSync(resolve(apiWwwrootDir, entry), { recursive: true, force: true });
}

function copyMissingTree(srcDir, destDir) {
  if (!existsSync(srcDir)) return;
  if (!existsSync(destDir)) {
    mkdirSync(destDir, { recursive: true });
  }
  for (const entry of readdirSync(srcDir, { withFileTypes: true })) {
    const srcPath = resolve(srcDir, entry.name);
    const destPath = resolve(destDir, entry.name);
    if (entry.isDirectory()) {
      copyMissingTree(srcPath, destPath);
    } else if (!existsSync(destPath)) {
      copyFileSync(srcPath, destPath);
    }
  }
}

for (const entry of readdirSync(clientDistDir, { withFileTypes: true })) {
  const srcPath = resolve(clientDistDir, entry.name);
  const destPath = resolve(apiWwwrootDir, entry.name);
  if (entry.name === "images" && entry.isDirectory()) {
    copyMissingTree(srcPath, destPath);
    continue;
  }
  cpSync(srcPath, destPath, { recursive: true, force: true });
}

console.log(`Copied client-vite/dist -> ${apiWwwrootDir}`);
