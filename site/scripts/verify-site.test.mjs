import { test } from 'node:test';
import assert from 'node:assert/strict';
import { cp, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { verifySite } from './verify-site.mjs';

test('public-site validation accepts the deployment and rejects broken discovery metadata', async t => {
  const fixture = await mkdtemp(path.join(tmpdir(), 'restart-site-'));
  const source = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../dist');
  await cp(source, fixture, { recursive: true });
  const guide = 'guides/windows-11-live-tiles/index.html';
  const mutate = async (file, change, message) => {
    const filename = path.join(fixture, file);
    const original = await readFile(filename, 'utf8');
    try {
      const altered = change(original);
      assert.notEqual(altered, original, 'The rejection fixture must change its input.');
      await writeFile(filename, altered);
      await assert.rejects(verifySite(fixture), message);
    } finally { await writeFile(filename, original); }
  };
  try {
    await t.test('valid deployment', async () => assert.equal((await verifySite(fixture)).pages, 5));
    await t.test('project-prefix link', () => mutate(guide, html => html.replace('href="../../developers/"', 'href="/developers/"'), /project prefix/));
    await t.test('missing cross-page anchor', () => mutate(guide, html => html.replace('../full-screen-start-menu/#set-up', '../full-screen-start-menu/#not-a-section'), /Missing link anchor/));
    await t.test('wrong canonical', () => mutate(guide, html => html.replace('rel="canonical" href="https://lastresolution.github.io/ReStart/guides/windows-11-live-tiles/', 'rel="canonical" href="https://lastresolution.github.io/guides/windows-11-live-tiles/'), /Canonical URL mismatch/));
    await t.test('duplicate description', () => mutate(guide, html => html.replace('<meta name="description"', '<meta name="description" content="Duplicate description"><meta name="description"'), /duplicate metadata description/));
    await t.test('missing alt text', () => mutate(guide, html => html.replace('alt="Four ReStart web samples:', 'not-alt="Four ReStart web samples:'), /Missing image alternative/));
    await t.test('mismatched article headline', () => mutate(guide, html => html.replace('"headline": "How to Bring Live Tiles Back to Windows 11"', '"headline": "A different article"'), /Article headline mismatch/));
    await t.test('incorrect breadcrumb parent', () => mutate(guide, html => html.replace('"item": "https://lastresolution.github.io/ReStart/guides/"', '"item": "https://lastresolution.github.io/ReStart/missing/"'), /Invalid breadcrumb trail/));
    await t.test('malformed JSON-LD', () => mutate(guide, html => html.replace('"@context": "https://schema.org",', '"@context": invalid,'), /JSON|Unexpected/));
    await t.test('incomplete sitemap', () => mutate('sitemap.xml', xml => xml.replace('  <url><loc>https://lastresolution.github.io/ReStart/guides/windows-11-live-tiles/</loc></url>\n', ''), /Sitemap entries/));
  } finally {
    // This exact directory was created by mkdtemp above and contains only this test's deployment copy.
    await rm(fixture, { recursive: true, force: true });
  }
});
