# ReStart project website

Buildless, English-first landing and developer pages for GitHub Pages. The deployment workflow publishes only the verified contents of `site/dist`.

## Local preview

From the repository root:

```powershell
python -m http.server 4173 --bind 127.0.0.1 --directory site/dist
```

Open `http://127.0.0.1:4173/` for the landing page or `/developers/` for the developer guide. All local links use relative paths, so the same files work under a project Pages prefix such as `/ReStart/`.

The site has no build step, third-party JavaScript, external fonts, analytics, contact forms or backend. Theme selection is kept in browser local storage; if storage is unavailable, switching still works for the current page. System mode follows the OS preference. Images and the silent product video are local assets.

## Motion and presentation

The landing page uses staggered tile-shaped entrances, one-time scroll reveals, a subtle pointer-driven product tilt, a reading-progress line, and transitions for themes, feature tabs and the screenshot viewer. The developer guide shares the same motion system. No background animation runs indefinitely, and the video plays only on request.

Motion follows the system's reduced-motion preference, including changes while the page is open; there is no separate website setting. Content and navigation remain readable without JavaScript. Keyboard focus immediately exposes any element waiting for its entrance animation; touch devices do not receive pointer tilt.

The full-screen screenshot viewer fits the complete image inside the viewport. Its close control, Escape key and backdrop dismissal restore focus to the originating button. The hero keeps “full-screen” together at narrow widths, and its ambient background fades to transparent before reaching the content edges.

## Review checks

```powershell
node site/scripts/verify-site.mjs
```

Check both pages at desktop, tablet and narrow mobile widths, with light/dark/system themes and reduced motion. Exercise the mobile menu, keyboard tabs, screenshot dialog, FAQ disclosures, video and all download/support links. Keep the nineteen web packages, Tile Studio controls and release availability synchronized with the app documentation.

## Deployment

Configure the repository's Pages source as GitHub Actions. `.github/workflows/pages.yml` verifies relative links and static assets, then deploys on a change to `site/**` on `main`, or through a manual dispatch. The repository root and development files are excluded. Review the `lastresolution.github.io/ReStart/` social metadata if a custom domain is selected.

The site uses the project's existing MIT license. Product screenshots show an English synthetic demonstration environment. Illustrated app names/icons remain their owners' trademarks; see `docs/third-party.md`. The promotional social image is an AI-assisted composition of the project identity and demonstration UI.
