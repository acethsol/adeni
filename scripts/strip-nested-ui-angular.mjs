/**
 * packages/ui is source-consumed by Angular apps. A nested
 * packages/ui/node_modules/@angular creates a second @angular/core in the
 * Discover SSR bundle and breaks inject() (NG0203).
 */
import { rmSync, existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const nested = join(root, "packages", "ui", "node_modules");
if (existsSync(nested)) {
  rmSync(nested, { recursive: true, force: true });
  console.log("Removed packages/ui/node_modules (avoid duplicate @angular for SSR).");
}
