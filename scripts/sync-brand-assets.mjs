import { cpSync, existsSync, mkdirSync, readdirSync, rmSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const brandPkg = join(root, "packages", "brand", "assets");
const apps = ["discover", "portal", "admin"];

for (const app of apps) {
  const publicDir = join(root, "apps", app, "public");
  mkdirSync(publicDir, { recursive: true });

  const brandDest = join(publicDir, "brand");
  rmSync(brandDest, { recursive: true, force: true });
  cpSync(join(brandPkg, "brand"), brandDest, { recursive: true });

  const pwaDest = join(publicDir, "pwa");
  rmSync(pwaDest, { recursive: true, force: true });
  if (existsSync(join(brandPkg, "pwa"))) {
    cpSync(join(brandPkg, "pwa"), pwaDest, { recursive: true });
  }

  const faviconDir = join(brandPkg, "favicon");
  if (existsSync(faviconDir)) {
    for (const name of readdirSync(faviconDir)) {
      cpSync(join(faviconDir, name), join(publicDir, name));
    }
  }
}

console.log("Synced @adeni/brand assets into discover, portal, and admin public/ folders.");
