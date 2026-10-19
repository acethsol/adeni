/**
 * Source-consumed workspaces must not keep nested @angular copies — a second
 * @angular/core/router breaks inject() (NG0203) and RouterLink imports (NG3004).
 */
import { existsSync, readdirSync, rmSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");

const hoistedAngular = join(root, "node_modules", "@angular");
const hoistedPackages = existsSync(hoistedAngular)
  ? readdirSync(hoistedAngular, { withFileTypes: true })
      .filter((entry) => entry.isDirectory())
      .map((entry) => entry.name)
  : [];

function removeNestedAngular(workspaceDir) {
  const nestedAngular = join(workspaceDir, "node_modules", "@angular");
  if (!existsSync(nestedAngular)) {
    return;
  }

  for (const pkg of hoistedPackages) {
    const nestedPkg = join(nestedAngular, pkg);
    if (existsSync(nestedPkg)) {
      rmSync(nestedPkg, { recursive: true, force: true });
      console.log(`Removed ${nestedPkg} (use hoisted @angular/${pkg}).`);
    }
  }
}

removeNestedAngular(join(root, "packages", "ui"));

const appsDir = join(root, "apps");
if (existsSync(appsDir)) {
  for (const app of readdirSync(appsDir, { withFileTypes: true })) {
    if (app.isDirectory()) {
      removeNestedAngular(join(appsDir, app.name));
    }
  }
}
