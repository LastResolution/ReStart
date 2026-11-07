import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { updateContributors } from './update-contributors.mjs';

const account = { login: 'Example-User', type: 'User', contributions: 1, avatar_url: 'https://avatars.githubusercontent.com/u/123?v=4', email: 'ignored@example.invalid', name: 'Never export this' };
const avatar = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aP1cAAAAASUVORK5CYII=', 'base64');

test('contributor refresh is static, bounded and keeps its snapshot on API failures', async t => {
  const directory = await mkdtemp(path.join(tmpdir(), 'restart-contributors-'));
  const destination = path.join(directory, 'contributors.svg');
  const previous = '<svg><image href="data:image/png;base64,snapshot"/></svg>';
  const reset = () => writeFile(destination, previous);
  const json = data => new Response(JSON.stringify(data));
  try {
    await t.test('one API request, token kept off avatar requests and only public logins exported', async () => {
      await reset();
      const requests = [];
      const request = async (url, options) => {
        requests.push({ url, options });
        return requests.length === 1 ? json([account, { type: 'Bot', login: 'ignored-bot', contributions: 1 }]) : new Response(avatar);
      };
      assert.deepEqual(await updateContributors({ destination, request, token: 'test-only-credential' }), { updated: true, count: 1 });
      assert.equal(requests.filter(row => row.url.startsWith('https://api.github.com/')).length, 1);
      assert.equal(requests[0].options.headers.Authorization, 'Bearer test-only-credential');
      assert.equal(requests[1].options.headers, undefined);
      assert.ok(requests.every(row => row.options.redirect === 'error' && row.options.signal));
      const image = await readFile(destination, 'utf8');
      assert.ok(image.includes('Example-User') && image.includes('data:image/png;base64,'));
      assert.doesNotMatch(image, /test-only-credential|ignored@example|Never export|https?:\/\/(?!www\.w3\.org)/);
    });
    for (const status of [403, 429, 500]) await t.test(`HTTP ${status} preserves the bundled image without retries`, async () => {
      await reset(); let calls = 0;
      const result = await updateContributors({ destination, request: async () => { calls++; return new Response('', { status }); } });
      assert.equal(result.updated, false); assert.equal(calls, 1); assert.equal(await readFile(destination, 'utf8'), previous);
    });
    for (const [name, data] of [
      ['empty list', []], ['malformed list', {}], ['injected login', [{ ...account, login: '<script>' }]],
      ['external host', [{ ...account, avatar_url: 'https://example.invalid/private' }]],
      ['embedded credentials', [{ ...account, avatar_url: 'https://user:secret@avatars.githubusercontent.com/u/123' }]]
    ]) await t.test(`${name} preserves the snapshot`, async () => {
      await reset(); let calls = 0;
      assert.equal((await updateContributors({ destination, request: async () => { calls++; return json(data); } })).updated, false);
      assert.equal(calls, 1); assert.equal(await readFile(destination, 'utf8'), previous);
    });
    await t.test('network failure and unsafe/oversized avatar bodies preserve the snapshot', async () => {
      for (const body of [new Error('network'), '<svg onload="alert(1)"/>', Buffer.alloc(128_001)]) {
        await reset(); let calls = 0;
        const request = async () => { if (++calls === 1) return json([account]); if (body instanceof Error) throw body; return new Response(body); };
        assert.equal((await updateContributors({ destination, request })).updated, false);
        assert.equal(await readFile(destination, 'utf8'), previous);
      }
    });
    await t.test('larger communities produce a bounded image with the leading contributors', async () => {
      await reset(); let calls = 0;
      const people = Array.from({ length: 64 }, (_, index) => ({ ...account, login: `Contributor${index}` }));
      const portrait = Buffer.alloc(96_000); avatar.copy(portrait);
      const request = async () => ++calls === 1 ? json(people) : new Response(portrait);
      const result = await updateContributors({ destination, request });
      assert.equal(result.updated, true);
      assert.ok(result.count > 0 && result.count < 64);
      const image = await readFile(destination, 'utf8');
      assert.ok(Buffer.byteLength(image) < 1_000_000 && image.includes('Contributor0'));
    });
  } finally {
    // Only the exact disposable fixture created by mkdtemp above is removed.
    await rm(directory, { recursive: true, force: true });
  }
});
