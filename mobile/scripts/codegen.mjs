// Generates a typed API client model from the OpenAPI contract.
// Usage: npm run codegen
import { execFileSync } from 'node:child_process';
import { mkdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const spec = resolve(here, '../../specs/001-mini-freight-requests/contracts/openapi.yaml');
const out = resolve(here, '../src/services/api/generated.ts');

mkdirSync(dirname(out), { recursive: true });

console.log(`[codegen] ${spec}\n        -> ${out}`);
const bin = process.platform === 'win32' ? 'npx.cmd' : 'npx';
execFileSync(bin, ['openapi-typescript', spec, '-o', out], { stdio: 'inherit' });
console.log('[codegen] done');
