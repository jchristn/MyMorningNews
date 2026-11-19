#!/usr/bin/env node
/**
 * Generate the app icons, the Android adaptive icon layers, the notification icon, and the splash images from the
 * brand files in the repository root (assets/glyph.png and assets/logo.png).
 *
 * Usage (from mobile/):
 *   npm run assets
 *
 * Output: mobile/assets/images/
 *   icon.png                   1024x1024, opaque white background, glyph centered (iOS and store icon)
 *   adaptive-foreground.png    1024x1024, transparent, glyph inside the adaptive icon safe zone
 *   adaptive-background.png    1024x1024, solid white
 *   adaptive-monochrome.png    1024x1024, white silhouette (Android 13 themed icons)
 *   notification-icon.png      96x96, white silhouette on transparent (Android status bar icon)
 *   splash-icon.png            1024x1024, transparent glyph (light splash on white)
 *   splash-icon-dark.png       1024x1024, transparent glyph (dark splash on #121417)
 *   glyph.png                  256x256, transparent glyph (sign-in header, player artwork)
 *   logo.png                   1086x362, transparent wordmark (About screen)
 *
 * Images are produced with jimp-compact (pure JavaScript, pinned in devDependencies), so no native toolchain is
 * needed. Re-run after the brand files change and commit the output.
 */
const fs = require('fs');
const path = require('path');
const Jimp = require('jimp-compact');

const repoAssets = path.resolve(__dirname, '..', '..', 'assets');
const outDir = path.resolve(__dirname, '..', 'assets', 'images');

/** Background behind the glyph on the opaque icon and the adaptive icon. */
const IconBackground = '#ffffff';

/** Share of the canvas the glyph may use, per output. */
const Scale = Object.freeze({ icon: 0.78, adaptive: 0.6, notification: 0.9, splash: 0.9, glyph: 1 });

async function fitted(source, width, height, scale, background) {
  const w = Math.round(width * scale);
  const h = Math.round(height * scale);
  const art = source.clone().contain(w, h);
  const base = new Jimp(width, height, background ? Jimp.cssColorToHex(background) : 0x00000000);
  return base.composite(art, Math.round((width - w) / 2), Math.round((height - h) / 2));
}

function silhouette(image) {
  const out = image.clone();
  out.scan(0, 0, out.bitmap.width, out.bitmap.height, function paint(_x, _y, idx) {
    this.bitmap.data[idx] = 255;
    this.bitmap.data[idx + 1] = 255;
    this.bitmap.data[idx + 2] = 255;
  });
  return out;
}

async function generate() {
  const glyphPath = path.join(repoAssets, 'glyph.png');
  const logoPath = path.join(repoAssets, 'logo.png');
  for (const file of [glyphPath, logoPath]) {
    if (!fs.existsSync(file)) throw new Error('Brand file not found: ' + file);
  }
  const glyph = await Jimp.read(glyphPath);
  const logo = await Jimp.read(logoPath);
  fs.mkdirSync(outDir, { recursive: true });

  const outputs = {
    'icon.png': await fitted(glyph, 1024, 1024, Scale.icon, IconBackground),
    'adaptive-foreground.png': await fitted(glyph, 1024, 1024, Scale.adaptive),
    'adaptive-background.png': new Jimp(1024, 1024, Jimp.cssColorToHex(IconBackground)),
    'adaptive-monochrome.png': silhouette(await fitted(glyph, 1024, 1024, Scale.adaptive)),
    'notification-icon.png': silhouette(await fitted(glyph, 96, 96, Scale.notification)),
    'splash-icon.png': await fitted(glyph, 1024, 1024, Scale.splash),
    'splash-icon-dark.png': await fitted(glyph, 1024, 1024, Scale.splash),
    'glyph.png': await fitted(glyph, 256, 256, Scale.glyph),
    'logo.png': await fitted(logo, 1086, 362, 1),
  };
  const written = [];
  for (const [name, image] of Object.entries(outputs)) {
    const file = path.join(outDir, name);
    await image.writeAsync(file);
    written.push(file);
  }
  return written;
}

module.exports = { generate };

if (require.main === module) {
  generate()
    .then((files) => {
      for (const file of files) process.stdout.write('wrote ' + path.relative(process.cwd(), file) + '\n');
    })
    .catch((error) => {
      process.stderr.write(error.message + '\n');
      process.exit(1);
    });
}
