# ReStart project website

Buildless, English-first landing, developer and Guides pages for GitHub Pages. The deployment workflow publishes only the verified contents of `site/dist`.

## Local preview

From the repository root:

```powershell
python -m http.server 4173 --bind 127.0.0.1 --directory site/dist
```

Open `http://127.0.0.1:4173/` for the landing page, `/developers/` for the developer guide or `/guides/` for the article index. All local links use relative paths, so the same files work under a project Pages prefix such as `/ReStart/`.

To exercise the project prefix directly, run `python site/scripts/preview.py` instead, then open `http://127.0.0.1:4173/ReStart/`. This optional standard-library preview server also exposes isolated `/capture/orbit/`, `/capture/cpu/`, `/capture/memory/`, `/capture/calendar/`, `/capture/weather-scene/` and `/capture/canvas-garden/` pages for refreshing the gallery. Each renders the actual bundled HTML/CSS/JavaScript at 720×720 using its scale variable and fixed English sample context. Capture the top-left 720×720 region without resizing an old image; review it and replace only the matching PNG. These authoring fixtures are outside `dist`, make no provider requests and never read personal app settings or files.

The site has no application build step, third-party JavaScript, external fonts, analytics, contact forms or backend. Theme selection is kept in browser local storage; if storage is unavailable, switching still works for the current page. System mode follows the OS preference. Images and the silent product video are local assets.

## Contributor recognition

All eight READMEs, the landing page and the developer guide share `dist/assets/contributors.svg`. Pages refreshes this self-contained image during production deployments and once a week. The refresh makes one read-only GitHub REST request for public contributors, then downloads up to 64 public account avatars from GitHub's image host. Only public usernames and raster avatar pixels enter the SVG; anonymous identities, email addresses, real-name fields and bots are excluded. The full contributor graph stays one click away.

Visitors make no GitHub API or third-party avatar requests. README images are served from the project's Pages URL, so they update without bot commits. Pages uses its ephemeral read-only `GITHUB_TOKEN` only for `api.github.com`; no personal token, paid service or new secret is required. Public-repository standard GitHub-hosted runners are free. API requests are still rate-limited, but a timeout, rate limit, invalid response or avatar failure retains the checked-in image instead of breaking the deployment. Refreshes have a 30-second overall deadline, one API page, no retries and bounded response/image sizes. The generated SVG has no scripts or external resources. Pull-request checks use the bundled image and offline response fixtures rather than live API calls.

Run `node site/scripts/update-contributors.mjs` to refresh the checked-in fallback deliberately, and `node --test site/scripts/update-contributors.test.mjs` to check successful refresh, credential isolation and failure behavior. The normal Pages workflow runs the refresh, static verification and both test files before deploying. GitHub and README image caches may delay a newly merged contributor's appearance. GitHub's [rate-limit documentation](https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api) and [Actions billing documentation](https://docs.github.com/en/billing/concepts/product-billing/github-actions) describe the current limits and public-runner pricing.

## Motion and presentation

The landing page uses staggered tile-shaped entrances, one-time scroll reveals, a subtle pointer-driven product tilt, a reading-progress line, and transitions for themes, feature tabs and the screenshot viewer. The developer guide shares the same motion system. No background animation runs indefinitely, and the video plays only on request.

Motion follows the system's reduced-motion preference, including changes while the page is open; there is no separate website setting. Content and navigation remain readable without JavaScript. Keyboard focus immediately exposes any element waiting for its entrance animation; touch devices do not receive pointer tilt.

The full-screen screenshot viewer fits the complete image inside the viewport. Its close control, Escape key and backdrop dismissal restore focus to the originating button. The hero keeps “full-screen” together at narrow widths, and its ambient background fades to transparent before reaching the content edges.

## Review checks

```powershell
node site/scripts/verify-site.mjs
node --test site/scripts/verify-site.test.mjs
```

Check all five pages at desktop, tablet and narrow mobile widths, with light/dark/system themes and reduced motion. Exercise the mobile menu, keyboard tabs, screenshot dialog, FAQ disclosures, video, article contents/related links and download/support links. Keep the nineteen web packages, Tile Studio controls and release availability synchronized with the app documentation.

The verifier discovers deployed HTML recursively. It checks every page against the sitemap, canonical and social URLs, unique titles/descriptions, image alternatives/dimensions, project-relative links and fragment targets. Guides also require matching Article/CollectionPage and BreadcrumbList structured data, visible dates and author attribution. Gallery images must be at least 720×720, with a combined budget below 1 MB. Rejection fixtures cover broken metadata, JSON-LD, sitemap entries, images and links; they use a disposable temporary deployment copy. Both checks run on Pages pull requests and before deployment.

## Writing Guides

The article index lives in `dist/guides/index.html`; each article is a plain `index.html` in its own descriptive directory. `assets/guides.css` adds readable prose, a desktop contents sidebar, mobile contents and article cards using the shared theme tokens. There is no content-fetching script or client-side routing. All article text and links are present in the initial HTML, including without JavaScript.

The two guides answer different questions: installing a full-screen launcher, and adding/customizing live tiles. Follow [Google's people-first content guidance](https://developers.google.com/search/docs/fundamentals/creating-helpful-content): answer a concrete reader question, add useful setup/troubleshooting detail and avoid repetitive keyword-driven pages. Cite primary Microsoft sources for Windows behavior and check ReStart claims against the source, README and released package. Identify the covered release and project authorship. Describe the optional Windows tile compatibility reader as a supported subset, never a restoration of retired notification services. Do not imply that a default weather location is detected or that untested devices were tested.

When materially revising an article, update its visible coverage/date and JSON-LD together. Preserve its original publication date. Do not change dates just to look fresh. New pages need unique descriptions, canonical/OG/Twitter metadata, breadcrumbs, source links, related reading and an appropriate download route. Add the page to the sitemap and collection schema, then verify links and rendering. Existing images use synthetic English data; review them visually before reuse. Search indexing, rich results and rankings are controlled by the search engines and are not guaranteed by markup.

## Search discovery

The public sitemap is `https://lastresolution.github.io/ReStart/sitemap.xml`. It lists the landing page, developer guide, Guides index and both articles using their canonical URLs. It can be submitted directly to search engines. Update the sitemap when adding or removing a public page; do not add guessed modification dates or unpublished URLs.

The landing page contains the maintainer-provided Google Search Console and Bing Webmaster Tools verification meta tags. These are public ownership markers, not login credentials or API keys. Keep them in the deployed HTML after verification. In Search Console, use the URL-prefix property `https://lastresolution.github.io/ReStart/`, complete verification after deployment, then submit `sitemap.xml`. Submit the same sitemap in Bing Webmaster Tools.

A crawler's `robots.txt` must be at the hostname root, `https://lastresolution.github.io/robots.txt`. A file inside this project's `/ReStart/` directory does not control crawling. This project does not deploy a misleading project-level robots file; sitemap submission works independently of one. No analytics or third-party tracking script is needed for either ownership check.

## Deployment

Configure the repository's Pages source as GitHub Actions. `.github/workflows/pages.yml` verifies the site and rejection fixtures on pull requests, then deploys on a change to `site/**` on `main`, or through a manual dispatch. Pull requests do not deploy and have separate concurrency groups so a PR check cannot cancel a production deployment. The repository root and development files are excluded. Review the `lastresolution.github.io/ReStart/` social metadata if a custom domain is selected.

The site uses the project's existing MIT license. Product screenshots show an English synthetic demonstration environment. Illustrated app names/icons remain their owners' trademarks; see `docs/third-party.md`. The promotional social image is an AI-assisted composition of the project identity and demonstration UI.

All app screenshots, screenshot download links and the product video follow the site theme. Refresh both `render-promo.ps1 -Theme Dark` and `-Theme Light`; keep the same duration and scene timing so switching theme preserves playback position. `test-studio.ps1` exports paired high-resolution gallery images using the actual browser host. The static verifier checks both theme targets, and the script tests cover media switching without automatic playback.
