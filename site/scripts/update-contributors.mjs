import { readFile, writeFile, rename, rm } from 'node:fs/promises';
import { randomUUID } from 'node:crypto';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const output = new URL('../dist/assets/contributors.svg', import.meta.url);
const endpoint = 'https://api.github.com/repos/LastResolution/ReStart/contributors?per_page=100&anon=false';
const png = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);

async function boundedBody(response, limit) {
  if (!response.ok || !response.body) throw new Error('Contributor resource unavailable.');
  const chunks = [];
  let bytes = 0;
  for await (const chunk of response.body) {
    bytes += chunk.length;
    if (bytes > limit) throw new Error('Contributor resource exceeds its bound.');
    chunks.push(chunk);
  }
  return Buffer.concat(chunks);
}

function imageType(bytes) {
  if (bytes.subarray(0, 8).equals(png)) return 'image/png';
  if (bytes[0] === 255 && bytes[1] === 216 && bytes[2] === 255) return 'image/jpeg';
  if (bytes.toString('ascii', 0, 4) === 'RIFF' && bytes.toString('ascii', 8, 12) === 'WEBP') return 'image/webp';
  throw new Error('Unsupported contributor avatar.');
}

/**
 * Refresh a self-contained static image using one GitHub API request and public avatars.
 * @param {object} options Optional build-only transport, credential and destination overrides.
 * @param {typeof fetch} options.request HTTP transport; tests inject responses without network access.
 * @param {string} options.token Ephemeral read-only Actions token, used only for api.github.com.
 * @param {string|URL} options.destination Existing SVG snapshot to replace only after complete validation.
 * @returns {Promise<{updated: boolean, count?: number}>} Refresh result; unavailable data retains the snapshot.
 */
export async function updateContributors({ request = fetch, token = process.env.GITHUB_TOKEN, destination = output } = {}) {
  const filename = destination instanceof URL ? fileURLToPath(destination) : path.resolve(destination);
  const temporary = `${filename}.${randomUUID()}.tmp`;
  // Fail closed when no fallback exists: the site must never ship a broken image.
  const previous = await readFile(destination, 'utf8');
  if (!previous.includes('<svg') || !previous.includes('data:image/')) throw new Error('Missing contributor snapshot.');
  try {
    const signal = AbortSignal.timeout(30_000);
    const headers = { Accept: 'application/vnd.github+json', 'X-GitHub-Api-Version': '2026-03-10' };
    if (token) headers.Authorization = `Bearer ${token}`;
    const response = await request(endpoint, { headers, signal, redirect: 'error' });
    const data = JSON.parse((await boundedBody(response, 256_000)).toString('utf8'));
    if (!Array.isArray(data) || data.length > 100) throw new Error('Invalid contributor response.');
    const people = data.filter(person => person.type === 'User' && Number.isSafeInteger(person.contributions) && person.contributions > 0);
    if (!people.length) throw new Error('Empty contributor response.');
    const seen = new Set();
    const portraits = [];
    let portraitBytes = 0;
    for (const person of people.slice(0, 64)) {
      if (typeof person.login !== 'string' || !/^[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?$/.test(person.login)
          || seen.has(person.login.toLowerCase())) throw new Error('Invalid public contributor name.');
      seen.add(person.login.toLowerCase());
      const avatar = new URL(person.avatar_url);
      if (avatar.protocol !== 'https:' || avatar.hostname !== 'avatars.githubusercontent.com' || avatar.port || avatar.username || avatar.password
          || !/^\/u\/\d+$/.test(avatar.pathname)) throw new Error('Unexpected avatar address.');
      avatar.search = '?s=96&v=4'; avatar.hash = '';
      // No Authorization header is sent to the image host. Redirects are refused.
      const bytes = await boundedBody(await request(avatar.href, { signal, redirect: 'error' }), 128_000);
      const uri = `data:${imageType(bytes)};base64,${bytes.toString('base64')}`;
      if (portraits.length && portraitBytes + uri.length > 900_000) break;
      portraitBytes += uri.length;
      portraits.push({ login: person.login, uri });
    }
    const columns = Math.min(portraits.length, 8), rows = Math.ceil(portraits.length / columns);
    const width = columns * 68 + 4, height = rows * 68 + 4;
    const images = portraits.map((person, index) => {
      const x = index % columns * 68 + 8, y = Math.floor(index / columns) * 68 + 8;
      return `<clipPath id="avatar-${index}"><circle cx="${x + 28}" cy="${y + 28}" r="28"/></clipPath><image x="${x}" y="${y}" width="56" height="56" clip-path="url(#avatar-${index})" href="${person.uri}"/><circle cx="${x + 28}" cy="${y + 28}" r="28" fill="none" stroke="#8797ad" stroke-opacity=".35"/>`;
    }).join('\n');
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}" role="img" aria-labelledby="title description">\n<title id="title">ReStart contributors</title>\n<desc id="description">${portraits.map(person => person.login).join(', ')}</desc>\n${images}\n</svg>\n`;
    if (Buffer.byteLength(svg) > 1_000_000) throw new Error('Contributor image exceeds its budget.');
    await writeFile(temporary, svg, { flag: 'wx' });
    await rename(temporary, filename);
    return { updated: true, count: portraits.length };
  } catch {
    // Never log response bodies, credentials or arbitrary remote metadata.
    return { updated: false };
  } finally {
    // Only the unique sibling file generated by this invocation is removed.
    await rm(temporary, { force: true });
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const result = await updateContributors();
  console.log(result.updated ? `Updated static contributor image (${result.count} public accounts).` : 'Contributor refresh unavailable; keeping the bundled snapshot.');
}
