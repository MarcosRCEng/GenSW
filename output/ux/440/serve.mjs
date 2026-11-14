import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { dirname, extname, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));
const port = Number(process.argv[2] ?? 5188);
if (!Number.isInteger(port) || port < 1024 || port > 65535) throw new Error('Porta inválida');
const mime = { '.html': 'text/html; charset=utf-8', '.css': 'text/css; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.md': 'text/plain; charset=utf-8', '.png': 'image/png' };
createServer(async (req, res) => {
  try {
    const path = decodeURIComponent(new URL(req.url, 'http://127.0.0.1').pathname);
    const target = resolve(root, '.' + (path === '/' ? '/index.html' : path));
    if (!target.startsWith(root + sep)) { res.writeHead(403); res.end(); return; }
    const data = await readFile(target);
    res.writeHead(200, { 'Content-Type': mime[extname(target)] ?? 'application/octet-stream', 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff' });
    res.end(data);
  } catch {
    res.writeHead(404); res.end('Arquivo não encontrado');
  }
}).listen(port, '127.0.0.1', () => console.log(`GenSW UX #440 — http://127.0.0.1:${port}/ — PID ${process.pid} — somente dados sintéticos`));
