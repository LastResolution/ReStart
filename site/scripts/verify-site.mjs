import { readFile, readdir, stat } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../dist');
const pages = ['index.html', 'developers/index.html'];
const canonicalUrls = pages.map(page => new URL(page === 'index.html' ? './' : 'developers/', 'https://lastresolution.github.io/ReStart/').href);
let checks = 0;
const require = (condition, message) => { if (!condition) throw new Error(message); checks++; };
for (const page of pages) {
  const filename = path.join(root, page);
  const html = await readFile(filename, 'utf8');
  require(/<html\b[^>]*lang="en"/.test(html), `Missing English language: ${page}`);
  require(/<title>[^<]+<\/title>/.test(html), `Missing title: ${page}`);
  require(/<h1\b/.test(html) && (html.match(/<h1\b/g) || []).length === 1, `Expected one main heading: ${page}`);
  require(html.match(/<link\s+rel="canonical"\s+href="([^"]+)"/)?.[1] === canonicalUrls[pages.indexOf(page)], `Canonical URL mismatch: ${page}`);
  if (page === 'index.html') {
    for (const name of ['google-site-verification', 'msvalidate.01'])
      require(new RegExp(`<meta name="${name.replace('.', '\\.')}" content="[A-Za-z0-9_-]+"`).test(html), `Missing public ownership marker: ${name}`);
  }
  const absoluteLinks = [...html.matchAll(/\bhref="(https:[^"]+)"/g)].map(match => new URL(match[1]));
  require(absoluteLinks.some(url => url.protocol === 'https:' && url.hostname === 'buymeacoffee.com' &&
    url.pathname === '/LastResolution' && !url.port && !url.username && !url.password), `Missing support link: ${page}`);
  const ids = [...html.matchAll(/\bid="([^"]+)"/g)].map(match => match[1]);
  require(new Set(ids).size === ids.length, `Duplicate element ID: ${page}`);
  for (const match of html.matchAll(/\b(?:href|src|data-light|data-dark)="([^"]+)"/g)) {
    const target = match[1];
    if (/^https:\/\//.test(target)) {
      const url = new URL(target);
      require(url.hostname !== 'localhost' && !url.hostname.endsWith('.localhost') && !url.username && !url.password,
        `Invalid external address: ${page}`);
      continue;
    }
    require(!/^\/|^[a-z]+:/i.test(target), `Link will break under a Pages project prefix: ${page}: ${target}`);
    const [relative, fragment] = target.split('#');
    let resolved = path.resolve(path.dirname(filename), decodeURIComponent(relative || path.basename(filename)));
    require(resolved === root || resolved.startsWith(root + path.sep), `Link escaped the static directory: ${target}`);
    if ((await stat(resolved)).isDirectory()) resolved = path.join(resolved, 'index.html');
    await stat(resolved);
    if (fragment) {
      const destination = await readFile(resolved, 'utf8');
      require(destination.includes(`id="${fragment}"`), `Missing link anchor: ${page}: ${target}`);
    }
    checks++;
  }
}
const sitemap = await readFile(path.join(root, 'sitemap.xml'), 'utf8');
require(sitemap.includes('xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"'), 'Missing sitemap namespace.');
const locations = [...sitemap.matchAll(/<loc>([^<]+)<\/loc>/g)].map(match => match[1]);
require(locations.length === canonicalUrls.length && new Set(locations).size === locations.length &&
  canonicalUrls.every(url => locations.includes(url)), 'Sitemap entries must match the public canonical pages.');
async function inspect(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const filename = path.join(directory, entry.name);
    if (entry.isDirectory()) { await inspect(filename); continue; }
    require(!entry.name.startsWith('.') || entry.name === '.nojekyll', `Unexpected hidden deployment file: ${entry.name}`);
    require(!/\.(?:map|pfx|key|log|json)$/i.test(entry.name), `Unexpected development file: ${entry.name}`);
    if (/\.(?:html|css|js)$/i.test(entry.name)) {
      const text = await readFile(filename, 'utf8');
      require(!/(?:ghp_|github_pat_)[a-z0-9_]{15,}/i.test(text), `Credential-like content in ${entry.name}`);
    }
  }
}
await inspect(root);
const social = await stat(path.join(root, 'assets/social-preview.jpg'));
require(social.size < 1_000_000, 'Social preview exceeds 1 MB.');
console.log(`Verified ${pages.length} English pages and ${checks} static deployment checks.`);
