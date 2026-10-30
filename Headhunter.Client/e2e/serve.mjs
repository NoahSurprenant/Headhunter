// Serves the production build (dist/headhunter.client/browser, from `npx ng build`) as the site does,
// on E2E_PORT (default 4300): the files, index.html for every other path (the app's routes), the
// API answered from e2e/mockApi.mts and the map tiles (/tiles/*) as a plain grey tile, so nothing
// leaves the machine. Compressed, as Cloudflare sends it. For Lighthouse (lighthouserc.cjs) and the
// layout test (e2e/layout.spec.ts).
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { extname, join, normalize } from 'node:path';
import { fileURLToPath } from 'node:url';
import { crc32, deflateSync, gzipSync } from 'node:zlib';
import { mockResponse } from './mockApi.mts';

const port = Number(process.env.E2E_PORT ?? 4300);
const root = fileURLToPath(new URL('../dist/headhunter.client/browser/', import.meta.url));

const TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json',
  '.txt': 'text/plain; charset=utf-8',
  '.ico': 'image/x-icon',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.svg': 'image/svg+xml',
  '.wasm': 'application/wasm',
  '.xml': 'application/xml',
  '.glb': 'model/gltf-binary',
  '.ktx2': 'image/ktx2',
};
const COMPRESS = /^(text\/|application\/(json|xml)|image\/svg)/;

/** A 256x256 grey PNG: every map tile. */
function greyTile() {
  const chunk = (type, data) => {
    const length = Buffer.alloc(4);
    length.writeUInt32BE(data.length);
    const body = Buffer.concat([Buffer.from(type), data]);
    const crc = Buffer.alloc(4);
    crc.writeUInt32BE(crc32(body));
    return Buffer.concat([length, body, crc]);
  };
  const header = Buffer.alloc(13);
  header.writeUInt32BE(256, 0);
  header.writeUInt32BE(256, 4);
  header[8] = 8; // bit depth
  header[9] = 0; // greyscale
  const rows = Buffer.alloc(257 * 256, 0xc8);
  for (let y = 0; y < 256; y++) rows[y * 257] = 0; // no filter
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', header),
    chunk('IDAT', deflateSync(rows)),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}
const tile = greyTile();

// The files, read (and compressed) once
const cache = new Map();
async function file(path) {
  if (!cache.has(path)) {
    cache.set(path, readFile(path).then(raw => {
      const type = TYPES[extname(path)] ?? 'application/octet-stream';
      return { type, raw, gzip: COMPRESS.test(type) ? gzipSync(raw) : null };
    }, () => null));
  }
  return cache.get(path);
}

function send(req, res, status, type, body, gzip = null, headers = {}) {
  const compressed = gzip && /\bgzip\b/.test(req.headers['accept-encoding'] ?? '');
  res.writeHead(status, {
    'Content-Type': type,
    ...(compressed ? { 'Content-Encoding': 'gzip' } : {}),
    Vary: 'Accept-Encoding',
    ...headers,
  });
  res.end(compressed ? gzip : body);
}

createServer(async (req, res) => {
  const url = new URL(req.url ?? '/', 'http://localhost');
  if (/^\/api\//i.test(url.pathname)) {
    const mocked = mockResponse(req.method ?? 'GET', url.pathname + url.search);
    const body = Buffer.from(mocked.body);
    return send(req, res, mocked.status, mocked.contentType, body, gzipSync(body));
  }
  if (url.pathname.startsWith('/tiles/'))
    return send(req, res, 200, 'image/png', tile, null, { 'Cache-Control': 'public, max-age=86400' });

  const path = normalize(join(root, decodeURIComponent(url.pathname)));
  const found = path.startsWith(root) && extname(path) ? await file(path) : null;
  // Hashed file names (outputHashing all): cached for good, as the site does
  const immutable = /-[A-Z0-9]{8}\.(js|css)$/.test(path) || path.includes('/assets/cesium/');
  if (found)
    return send(req, res, 200, found.type, found.raw, found.gzip, immutable ? { 'Cache-Control': 'public, max-age=31536000, immutable' } : {});
  if (extname(url.pathname))
    return send(req, res, 404, 'text/plain', Buffer.from('Not found'));
  const index = await file(join(root, 'index.html'));
  return send(req, res, 200, index.type, index.raw, index.gzip);
}).listen(port, '127.0.0.1', () => {
  // lighthouserc.cjs and playwright.config.ts wait for this line
  console.log(`Serving on http://127.0.0.1:${port}`);
});
