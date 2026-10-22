import { readFile, readdir, stat } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const defaultRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../dist');
const siteUrl = 'https://lastresolution.github.io/ReStart/';
const attributes = tag => Object.fromEntries([...tag.matchAll(/([\w:-]+)="([^"]*)"/g)].map(match => [match[1], match[2]]));
const tags = (html, name) => [...html.matchAll(new RegExp(`<${name}\\b[^>]*>`, 'g'))].map(match => attributes(match[0]));
const normalizedText = text => text.replaceAll('&amp;', '&').replace(/\s+/g, ' ').trim();

/**
 * Verify every deployed HTML file, including links under a GitHub Pages project prefix.
 * @param {string} directory Deployment root to read; defaults to this site's dist directory.
 * Relative paths resolve against the process working directory. Files are read without mutation.
 * @returns {Promise<{pages: number, checks: number}>} Number of discovered HTML pages and successful assertions.
 * @throws {Error} On the first invalid page, asset or sitemap entry, or an unreadable deployment file.
 */
export async function verifySite(directory = defaultRoot) {
  const root = path.resolve(directory);
  let checks = 0;
  const require = (condition, message) => { if (!condition) throw new Error(message); checks++; };
  async function filesIn(directory) {
    const files = [];
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      const filename = path.join(directory, entry.name);
      if (entry.isDirectory()) files.push(...await filesIn(filename));
      else files.push(filename);
    }
    return files;
  }
  const files = await filesIn(root);
  const pages = files.filter(filename => filename.endsWith('.html')).map(filename => path.relative(root, filename).replaceAll(path.sep, '/')).sort();
  require(['index.html', 'developers/index.html', 'guides/index.html', 'guides/full-screen-start-menu/index.html',
    'guides/windows-11-live-tiles/index.html'].every(page => pages.includes(page)), 'Missing a public page.');
  const canonicalUrls = pages.map(page => new URL(page.replace(/index\.html$/, ''), siteUrl).href);
  const titles = new Set(), descriptions = new Set();
  for (const [index, page] of pages.entries()) {
    const filename = path.join(root, page);
    const html = await readFile(filename, 'utf8');
    const meta = tags(html, 'meta');
    const getMeta = name => {
      const matches = meta.filter(item => item.name === name || item.property === name);
      require(matches.length === 1 && Boolean(matches[0].content?.trim()), `Missing or duplicate metadata ${name}: ${page}`);
      return matches[0].content;
    };
    require(/<!doctype html>/i.test(html) && /<html\b[^>]*lang="en"/.test(html), `Missing English HTML document: ${page}`);
    require(tags(html, 'main').length === 1, `Expected one main landmark: ${page}`);
    require(tags(html, 'h1').length === 1, `Expected one main heading: ${page}`);
    const title = html.match(/<title>([^<]+)<\/title>/)?.[1]?.trim();
    require(title && !titles.has(title), `Missing or duplicate page title: ${page}`); titles.add(title);
    const description = getMeta('description');
    require(description.length >= 60 && description.length <= 190 && !descriptions.has(description), `Missing or duplicate page description: ${page}`);
    descriptions.add(description);
    require(getMeta('viewport').includes('width=device-width'), `Missing responsive viewport: ${page}`);
    const canonical = tags(html, 'link').filter(link => link.rel === 'canonical');
    const url = canonicalUrls[index];
    require(canonical.length === 1 && canonical[0].href === url, `Canonical URL mismatch: ${page}`);
    require(getMeta('og:url') === url, `Open Graph URL mismatch: ${page}`);
    getMeta('og:title'); getMeta('og:description'); getMeta('og:image:alt');
    const ogImage = new URL(getMeta('og:image'));
    require(ogImage.href.startsWith(siteUrl + 'assets/'), `Social image must be a deployed asset: ${page}`);
    await stat(path.join(root, ogImage.href.slice(siteUrl.length)));
    require(getMeta('twitter:card') === 'summary_large_image', `Missing social card: ${page}`);
    if (page === 'index.html') {
      for (const name of ['google-site-verification', 'msvalidate.01'])
        require(/^[A-Za-z0-9_-]+$/.test(getMeta(name)), `Missing public ownership marker: ${name}`);
    }
    const links = tags(html, 'a');
    const external = links.filter(link => link.href?.startsWith('https://')).map(link => new URL(link.href));
    require(external.some(link => link.hostname === 'buymeacoffee.com' && link.pathname === '/LastResolution'), `Missing support link: ${page}`);
    const navigation = html.match(/<nav\b[^>]*id="primary-nav"[^>]*>([\s\S]*?)<\/nav>/)?.[1];
    require(navigation && tags(navigation, 'a').some(link => new URL(link.href, url).href === siteUrl + 'guides/') &&
      navigation.includes('>Guides</a>'), `Missing Guides navigation: ${page}`);
    const ids = [...html.matchAll(/\bid="([^"]+)"/g)].map(match => match[1]);
    require(new Set(ids).size === ids.length, `Duplicate element ID: ${page}`);
    const targets = [...html.matchAll(/\b(?:href|src|poster|data-light|data-dark)="([^"]+)"/g)].map(match => match[1]);
    for (const image of tags(html, 'img')) {
      require(Object.hasOwn(image, 'alt') && Number(image.width) > 0 && Number(image.height) > 0, `Missing image alternative or dimensions: ${page}`);
      if (image.srcset) for (const candidate of image.srcset.split(',')) {
        const [target, descriptor] = candidate.trim().split(/\s+/);
        require(/^[1-9][0-9]*(?:w|x)$/.test(descriptor), `Invalid responsive image candidate: ${page}`);
        targets.push(target);
      }
    }
    for (const target of targets) {
      if (target.startsWith('https://')) {
        const link = new URL(target.replaceAll('&amp;', '&'));
        require(link.hostname !== 'localhost' && !link.hostname.endsWith('.localhost') && !link.username && !link.password, `Invalid external address: ${page}`);
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
        require(destination.includes(`id="${decodeURIComponent(fragment)}"`), `Missing link anchor: ${page}: ${target}`);
      }
      checks++;
    }
    const isArticle = /^guides\/[^/]+\/index\.html$/.test(page);
    const schemas = [...html.matchAll(/<script type="application\/ld\+json">([\s\S]*?)<\/script>/g)].map(match => JSON.parse(match[1]));
    const graph = schemas.flatMap(schema => schema['@graph'] || [schema]);
    require(schemas.every(schema => schema['@context'] === 'https://schema.org'), `Invalid structured data context: ${page}`);
    require(getMeta('og:type') === (isArticle ? 'article' : 'website'), `Wrong social page type: ${page}`);
    if (page.startsWith('guides/')) {
      getMeta('twitter:title'); getMeta('twitter:description'); getMeta('twitter:image:alt');
      require(getMeta('twitter:image') === ogImage.href, `Twitter image mismatch: ${page}`);
      require(/<nav\b[^>]*aria-label="Breadcrumb"/.test(html), `Missing visible breadcrumbs: ${page}`);
      const breadcrumbs = graph.filter(schema => schema['@type'] === 'BreadcrumbList');
      require(breadcrumbs.length === 1, `Missing or duplicate breadcrumb schema: ${page}`);
      const items = breadcrumbs[0].itemListElement;
      require(items?.length === (isArticle ? 3 : 2) && items.every((item, i) => item['@type'] === 'ListItem' && item.position === i + 1 &&
        item.name && canonicalUrls.includes(item.item)) && items.at(-1).item === url, `Invalid breadcrumb trail: ${page}`);
      if (isArticle) {
        const articles = graph.filter(schema => schema['@type'] === 'Article');
        require(articles.length === 1, `Missing or duplicate Article schema: ${page}`);
        const article = articles[0];
        require(article.url === url && article.mainEntityOfPage?.['@id'] === url && article.inLanguage === 'en', `Article URL mismatch: ${page}`);
        require(article.headline === normalizedText(html.match(/<h1\b[^>]*>([^<]+)<\/h1>/)?.[1] || ''), `Article headline mismatch: ${page}`);
        require(article.description === description.replaceAll('&amp;', '&'), `Article description mismatch: ${page}`);
        require(article.author?.name && article.author?.url && article.publisher?.name && article.image?.length, `Missing article attribution or image: ${page}`);
        for (const image of article.image) {
          require(image.startsWith(siteUrl + 'assets/'), `Article image is not a deployed asset: ${page}`);
          await stat(path.join(root, image.slice(siteUrl.length)));
        }
        require(/^\d{4}-\d{2}-\d{2}$/.test(article.datePublished) && article.dateModified >= article.datePublished &&
          html.includes(`datetime="${article.datePublished}"`), `Invalid or invisible article dates: ${page}`);
        require(/aria-label="On this page"/.test(html) && /class="guide-related"/.test(html), `Missing contents or related reading: ${page}`);
        require(links.some(link => link.href === 'https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-x64.zip'), `Missing article download: ${page}`);
      } else {
        const collection = graph.find(schema => schema['@type'] === 'CollectionPage');
        const articleUrls = canonicalUrls.filter(link => /\/guides\/[^/]+\/$/.test(link));
        require(collection?.url === url && collection.hasPart?.length === articleUrls.length &&
          articleUrls.every(link => collection.hasPart.some(item => item.url === link)), `Guide collection mismatch: ${page}`);
      }
    }
  }
  const sitemap = await readFile(path.join(root, 'sitemap.xml'), 'utf8');
  require(sitemap.includes('xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"'), 'Missing sitemap namespace.');
  const locations = [...sitemap.matchAll(/<loc>([^<]+)<\/loc>/g)].map(match => match[1]);
  require(locations.length === canonicalUrls.length && new Set(locations).size === locations.length &&
    canonicalUrls.every(url => locations.includes(url)), 'Sitemap entries must match the public canonical pages.');
  for (const filename of files) {
    const name = path.basename(filename);
    require(!name.startsWith('.') || name === '.nojekyll', `Unexpected hidden deployment file: ${name}`);
    require(!/\.(?:map|pfx|key|log|json)$/i.test(name), `Unexpected development file: ${name}`);
    if (/\.(?:html|css|js)$/i.test(name)) {
      const text = await readFile(filename, 'utf8');
      require(!/(?:ghp_|github_pat_)[a-z0-9_]{15,}/i.test(text), `Credential-like content in ${name}`);
    }
  }
  let galleryBytes = 0;
  for (const name of ['orbit', 'cpu', 'memory', 'calendar', 'weather-scene', 'canvas-garden']) {
    const png = await readFile(path.join(root, `assets/tile-${name}.png`));
    require(png.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10])) &&
      png.readUInt32BE(16) >= 720 && png.readUInt32BE(20) >= 720, `Low-resolution tile gallery image: ${name}`);
    galleryBytes += png.length;
  }
  require(galleryBytes < 1_000_000, 'Tile gallery exceeds the combined 1 MB image budget.');
  require((await stat(path.join(root, 'assets/social-preview.jpg'))).size < 1_000_000, 'Social preview exceeds 1 MB.');
  return { pages: pages.length, checks };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const result = await verifySite();
  console.log(`Verified ${result.pages} English pages and ${result.checks} static deployment checks.`);
}
