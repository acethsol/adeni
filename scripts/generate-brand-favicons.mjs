/**
 * Builds favicon + PWA PNGs from canonical adeni-mark.png (transparent).
 * Does not alter mark artwork — scales and centers with alpha padding only.
 * Run after updating mark PNG; header logos still come from the branding zip.
 */
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import sharp from "sharp";
import pngToIco from "png-to-ico";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const markPath = join(root, "packages", "brand", "assets", "brand", "adeni-mark.png");
const faviconDir = join(root, "packages", "brand", "assets", "favicon");
const pwaDir = join(root, "packages", "brand", "assets", "pwa");

const transparent = { r: 0, g: 0, b: 0, alpha: 0 };

/** Fit mark inside square canvas with transparent padding (~12%). */
async function markSquare(size) {
  const inner = Math.round(size * 0.76);
  const mark = await sharp(markPath)
    .resize(inner, inner, { fit: "contain", background: transparent })
    .png()
    .toBuffer();

  return sharp({
    create: {
      width: size,
      height: size,
      channels: 4,
      background: transparent,
    },
  })
    .composite([{ input: mark, gravity: "centre" }])
    .png();
}

mkdirSync(faviconDir, { recursive: true });
mkdirSync(pwaDir, { recursive: true });

const sizes = [
  { name: "favicon-16.png", size: 16 },
  { name: "favicon-32.png", size: 32 },
  { name: "favicon-48.png", size: 48 },
  { name: "favicon-96.png", size: 96 },
  { name: "favicon-180.png", size: 180 },
  { name: "favicon-192.png", size: 192 },
  { name: "favicon-512.png", size: 512 },
  { name: "apple-touch-icon.png", size: 180 },
];

const pngBuffers = [];
for (const { name, size } of sizes) {
  const out = join(faviconDir, name);
  await (await markSquare(size)).toFile(out);
  if (size === 16 || size === 32) {
    pngBuffers.push(await sharp(out).png().toBuffer());
  }
}

const ico = await pngToIco(pngBuffers);
writeFileSync(join(faviconDir, "favicon.ico"), ico);

for (const size of [192, 512]) {
  await (await markSquare(size)).toFile(join(pwaDir, `icon-${size}.png`));
}

console.log("Generated transparent favicons and PWA icons from adeni-mark.png");
