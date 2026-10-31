import { runInNewContext } from 'node:vm';
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
    await t.test('incomplete sitemap', () => mutate('sitemap.xml', xml => xml.replace(/<url>\s*<loc>https:\/\/lastresolution\.github\.io\/ReStart\/guides\/windows-11-live-tiles\/<\/loc>\s*(?:<lastmod>[^<]+<\/lastmod>\s*)?<\/url>/, ''), /Sitemap entries/));
    await t.test('undersized gallery image', async () => {
      const filename = path.join(fixture, 'assets/tile-orbit.png');
      const original = await readFile(filename);
      try {
        const undersized = Buffer.from(original);
        undersized.writeUInt32BE(200, 16);
        await writeFile(filename, undersized);
        await assert.rejects(verifySite(fixture), /Low-resolution tile gallery image/);
      } finally { await writeFile(filename, original); }
    });
  } finally {
    // This exact directory was created by mkdtemp above and contains only this test's deployment copy.
    await rm(fixture, { recursive: true, force: true });
  }
});


test('theme changes update media and preserve a playing video across rapid switches', async () => {
  const source = await readFile(new URL('../dist/assets/site.js', import.meta.url), 'utf8');
  const functionSource = source.slice(source.indexOf('  const applyTheme ='), source.indexOf('  const changeTheme ='));
  const listeners = [];
  const videoSource = { src: 'assets/demo.mp4', getAttribute() { return this.src; }, setAttribute(_, value) { this.src = value; } };
  const video = {
    dataset: { dark: 'assets/demo.mp4', light: 'assets/demo-light.mp4', darkPoster: 'dark.png', lightPoster: 'light.png' },
    currentTime: 12.5, duration: 60, paused: false, ended: false, loads: 0, plays: 0,
    querySelector() { return videoSource; }, pause() { this.paused = true; },
    play() { this.paused = false; this.plays++; return Promise.resolve(); },
    load() { this.loads++; }, addEventListener(_, callback) { listeners.push(callback); }
  };
  const image = { dataset: { dark: 'dark.png', light: 'light.png' } };
  const link = { dataset: { dark: 'dark-full.png', light: 'light-full.png' } };
  const root = { dataset: {} };
  const apply = runInNewContext(`let preference; const themePlayback = new WeakMap(); ${functionSource}; applyTheme;`, {
    root, system: { matches: false }, localStorage: { setItem() {} },
    document: { querySelectorAll(selector) { return ({ '[data-theme-image]': [image], '[data-theme-link]': [link], '[data-theme-video]': [video] })[selector] || []; } }
  });
  apply('dark'); assert.equal(video.loads, 0);
  apply('light'); assert.equal(image.src, 'light.png'); assert.equal(link.href, 'light-full.png');
  assert.equal(video.poster, 'light.png'); assert.equal(videoSource.src, 'assets/demo-light.mp4');
  apply('dark'); listeners.shift()(); assert.equal(video.plays, 0, 'A stale metadata event must not resume the wrong source.');
  listeners.shift()(); assert.equal(video.currentTime, 12.5); assert.equal(video.plays, 1);
  video.paused = true; apply('light'); listeners.shift()(); assert.equal(video.plays, 1, 'A paused video must remain paused.');
  apply('system'); assert.equal(root.dataset.theme, 'light');
});
