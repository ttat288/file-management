import fs from 'node:fs';
import path from 'node:path';

function parseDotEnv(contents) {
  const env = {};
  for (const rawLine of contents.split(/\r?\n/)) {
    const line = rawLine.trim();
    if (!line || line.startsWith('#')) continue;
    const eq = line.indexOf('=');
    if (eq <= 0) continue;
    const key = line.slice(0, eq).trim();
    let value = line.slice(eq + 1).trim();
    if ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'"))) {
      value = value.slice(1, -1);
    }
    env[key] = value;
  }
  return env;
}

const projectRoot = path.resolve(process.cwd());
const envPath = path.join(projectRoot, '.env');
const envExamplePath = path.join(projectRoot, '.env.example');

let envContents = '';
if (fs.existsSync(envPath)) {
  envContents = fs.readFileSync(envPath, 'utf8');
} else if (fs.existsSync(envExamplePath)) {
  envContents = fs.readFileSync(envExamplePath, 'utf8');
}

const env = parseDotEnv(envContents);
const apiBaseUrl = env.API_BASE_URL?.trim() || '/api';

const outDir = path.join(projectRoot, 'src', 'environments');
fs.mkdirSync(outDir, { recursive: true });

const devOutPath = path.join(outDir, 'environment.development.ts');
const prodOutPath = path.join(outDir, 'environment.ts');

const devOut = `export const environment = {
  production: false,
  apiBaseUrl: ${JSON.stringify(apiBaseUrl)},
};
`;

const prodOut = `export const environment = {
  production: true,
  apiBaseUrl: ${JSON.stringify(apiBaseUrl)},
};
`;

fs.writeFileSync(devOutPath, devOut, 'utf8');
fs.writeFileSync(prodOutPath, prodOut, 'utf8');

console.log(`[env] Wrote ${path.relative(projectRoot, devOutPath)} and ${path.relative(projectRoot, prodOutPath)} (apiBaseUrl=${apiBaseUrl})`);

