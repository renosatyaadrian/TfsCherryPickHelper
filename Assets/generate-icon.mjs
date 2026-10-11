// Generates Assets/app.ico: a cherry-pick themed icon —
// a blue rounded square with a white branch graph (two commits merging into one).
// Multi-size ICO (16/32/48/256) with PNG-embedded entries (valid per ICO spec).
// Requires Node.js >= 18 (no external packages).
// Usage: node Assets/generate-icon.mjs

import { deflateSync } from "node:zlib";
import { writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const SIZES = [16, 32, 48, 256];
const BG = [37, 99, 235, 255]; // #2563EB — matches the app's primary button color
const FG = [255, 255, 255, 255];

// --- Minimal PNG encoder (RGBA, 8-bit) ---
function crc32(buf) {
  let c, table = crc32.table;
  if (!table) {
    table = crc32.table = new Int32Array(256);
    for (let n = 0; n < 256; n++) {
      c = n;
      for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
      table[n] = c;
    }
  }
  c = -1;
  for (const b of buf) c = table[(c ^ b) & 0xff] ^ (c >>> 8);
  return (c ^ -1) >>> 0;
}

function chunk(type, data) {
  const len = Buffer.alloc(4);
  len.writeUInt32BE(data.length);
  const body = Buffer.concat([Buffer.from(type, "ascii"), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(body));
  return Buffer.concat([len, body, crc]);
}

function encodePng(size, pixels) {
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(size, 0);
  ihdr.writeUInt32BE(size, 4);
  ihdr[8] = 8;  // bit depth
  ihdr[9] = 6;  // color type RGBA
  const raw = Buffer.alloc(size * (size * 4 + 1));
  for (let y = 0; y < size; y++) {
    raw[y * (size * 4 + 1)] = 0; // filter: none
    pixels.copy(raw, y * (size * 4 + 1) + 1, y * size * 4, (y + 1) * size * 4);
  }
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk("IHDR", ihdr),
    chunk("IDAT", deflateSync(raw)),
    chunk("IEND", Buffer.alloc(0)),
  ]);
}

// --- Icon drawing (per-pixel, supersampled 4x for smooth edges) ---
function drawIcon(size) {
  const SS = 4; // supersampling factor
  const S = size * SS;
  const px = Buffer.alloc(S * S * 4);

  const r = S / 5;                       // rounded-square corner radius
  const pad = S * 0.22;
  const x0 = pad, x1 = S - pad;
  const yTop = S * 0.30, yBot = S * 0.70, yMid = S * 0.50;
  const nodeR = S * 0.09;
  const mergeR = nodeR * 1.35;
  const lineW = S * 0.055;

  const inRoundedSquare = (x, y) => {
    const cx = Math.min(Math.max(x, r), S - r);
    const cy = Math.min(Math.max(y, r), S - r);
    return (x - cx) ** 2 + (y - cy) ** 2 <= r * r;
  };
  const inCircle = (x, y, cx, cy, rad) => (x - cx) ** 2 + (y - cy) ** 2 <= rad * rad;
  const nearSegment = (x, y, ax, ay, bx, by, w) => {
    const dx = bx - ax, dy = by - ay;
    const t = Math.max(0, Math.min(1, ((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy)));
    const px2 = ax + t * dx, py2 = ay + t * dy;
    return (x - px2) ** 2 + (y - py2) ** 2 <= (w / 2) ** 2;
  };

  for (let y = 0; y < S; y++) {
    for (let x = 0; x < S; x++) {
      if (!inRoundedSquare(x, y)) continue;
      const onGraph =
        nearSegment(x, y, x0, yTop, x1, yMid, lineW) ||
        nearSegment(x, y, x0, yBot, x1, yMid, lineW) ||
        inCircle(x, y, x0, yTop, nodeR) ||
        inCircle(x, y, x0, yBot, nodeR) ||
        inCircle(x, y, x1, yMid, mergeR);
      const c = onGraph ? FG : BG;
      const i = (y * S + x) * 4;
      px[i] = c[0]; px[i + 1] = c[1]; px[i + 2] = c[2]; px[i + 3] = c[3];
    }
  }

  // Downsample S -> size (box filter)
  const out = Buffer.alloc(size * size * 4);
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      let rS = 0, gS = 0, bS = 0, aS = 0;
      for (let sy = 0; sy < SS; sy++) {
        for (let sx = 0; sx < SS; sx++) {
          const i = ((y * SS + sy) * S + (x * SS + sx)) * 4;
          const a = px[i + 3];
          rS += px[i] * a; gS += px[i + 1] * a; bS += px[i + 2] * a; aS += a;
        }
      }
      const o = (y * size + x) * 4;
      if (aS > 0) {
        out[o] = Math.round(rS / aS);
        out[o + 1] = Math.round(gS / aS);
        out[o + 2] = Math.round(bS / aS);
        out[o + 3] = Math.round(aS / (SS * SS));
      }
    }
  }
  return out;
}

// --- Assemble ICO (PNG-embedded entries) ---
const dir = dirname(fileURLToPath(import.meta.url));
mkdirSync(join(dir, ".."), { recursive: true });

const images = SIZES.map((size) => ({ size, png: encodePng(size, drawIcon(size)) }));

const header = Buffer.alloc(6);
header.writeUInt16LE(0, 0);              // reserved
header.writeUInt16LE(1, 2);              // type: icon
header.writeUInt16LE(images.length, 4);  // count

const entries = [];
let offset = 6 + images.length * 16;
for (const { size, png } of images) {
  const e = Buffer.alloc(16);
  e[0] = size >= 256 ? 0 : size;          // width (0 = 256)
  e[1] = size >= 256 ? 0 : size;          // height
  e[2] = 0;                               // palette
  e[3] = 0;                               // reserved
  e.writeUInt16LE(1, 4);                  // color planes
  e.writeUInt16LE(32, 6);                 // bits per pixel
  e.writeUInt32LE(png.length, 8);         // data size
  e.writeUInt32LE(offset, 12);            // data offset
  offset += png.length;
  entries.push(e);
}

const ico = Buffer.concat([header, ...entries, ...images.map((i) => i.png)]);
const outPath = join(dir, "app.ico");
writeFileSync(outPath, ico);
console.log(`Written ${outPath} (${ico.length} bytes, sizes: ${SIZES.join("/")})`);
