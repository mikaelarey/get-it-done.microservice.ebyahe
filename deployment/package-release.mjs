// Generated from get-it-done.deployment/templates. Keep the protocol in sync.
import { createHash } from 'node:crypto';
import { existsSync, lstatSync, mkdirSync, readdirSync, readFileSync, writeFileSync, copyFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const target = JSON.parse(readFileSync(path.join(root, 'deployment/uat.json'), 'utf8'));
const source = path.resolve(root, target.buildPath);
const output = path.join(root, 'artifacts/uat-release');
const sourceCommit = process.env.GITHUB_SHA;
const runId = process.env.GITHUB_RUN_ID;
const runAttempt = process.env.GITHUB_RUN_ATTEMPT;
if (!/^[a-f0-9]{40}$/.test(sourceCommit ?? '') || !/^\d+$/.test(runId ?? '') || !/^\d+$/.test(runAttempt ?? '')) throw Error('GitHub commit, run ID and attempt are required.');
if (existsSync(output)) throw Error('Release output already exists. Use a clean build checkout.');
if (!source.startsWith(root + path.sep) || !['mfe', 'api'].includes(target.kind)) throw Error('Invalid build target.');
const files = [], names = new Set();
function scan(directory, relative = '') {
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const name = relative ? relative + '/' + entry.name : entry.name;
    const filename = path.join(directory, entry.name);
    const stats = lstatSync(filename);
    if (stats.isSymbolicLink() || (!stats.isFile() && !stats.isDirectory())) throw Error('Unsupported artifact entry: ' + name);
    if (/[<>:"\\|?*\x00-\x1f]/.test(entry.name) || /[ .]$/.test(entry.name) || /^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)/i.test(entry.name) || /^\.env(?:\.|$)/i.test(entry.name)) throw Error('Unsafe artifact entry: ' + name);
    if (names.has(name.toLowerCase())) throw Error('Case collision: ' + name);
    names.add(name.toLowerCase());
    if (stats.isDirectory()) scan(filename, name);
    else {
      // Runtime configuration belongs on the server. Never ship development,
      // production or UAT overrides from a developer machine into IIS.
      if (target.kind === 'api' && /(?:^|\/)appsettings\.[^/]+\.json$/i.test(name)) continue;
      if (/(?:^|\/)app_offline\.htm$/i.test(name)) throw Error('Offline markers must not be packaged.');
      files.push({ path: name, sha256: createHash('sha256').update(readFileSync(filename)).digest('hex'), length: stats.size });
    }
  }
}
scan(source);
const required = target.kind === 'mfe' ? ['index.html', 'web.config'] : ['web.config', target.assembly, target.assembly.replace(/\.dll$/, '.deps.json'), target.assembly.replace(/\.dll$/, '.runtimeconfig.json')];
for (const name of required) if (!files.some(file => file.path === name)) throw Error('Missing required publish file: ' + name);
if (target.kind === 'mfe' && (!files.some(file => /\.js$/.test(file.path)) || !files.some(file => /\.css$/.test(file.path)) || !/<base\s+href=["']\/["']\s*\/?\s*>/i.test(readFileSync(path.join(source, 'index.html'), 'utf8')))) throw Error('MFE must contain JS/CSS and use the domain root as its base URL.');
for (const file of files) {
  const destination = path.join(output, 'site', file.path);
  mkdirSync(path.dirname(destination), { recursive: true });
  copyFileSync(path.join(source, file.path), destination);
}
writeFileSync(path.join(output, 'manifest.json'), JSON.stringify({ schemaVersion: 2, application: target.repository, environment: 'UAT', kind: target.kind, name: target.name, hostName: target.hostName, sourceCommit, runId, runAttempt, files: files.sort((a,b) => a.path.localeCompare(b.path)) }, null, 2) + '\n');
console.log(`Packaged ${files.length} files for ${target.hostName} (${sourceCommit}).`);
