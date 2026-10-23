# Documentation images

README screenshots use **English across the whole image**, including app/tile names, live-tile titles, weekdays, dates, settings and example documents. They use curated common applications, fictional files, synthetic aggregate metrics and generated photo colors. Do not capture a personal desktop or export a user's installed-app list into documentation.

## Full launcher and animation

Build a Release executable, then run:

```powershell
./scripts/render-readme.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
```

The dedicated `--render-docs` entry point returns before normal instance management, tray, hooks, pin IPC and startup registration. It never shows a window. It uses a fresh ignored data directory, renders WPF controls directly, and stops its workers on exit. Real settings, wallpaper, chosen photos, data connections and document history are not used.

The renderer obtains actual icons for its fixed app selection from the local Shell and matches package/executable identities independently of localized names. It fails if a required application icon cannot be resolved; do not publish fallback website/globe/list icons under another application's name. Install the selected common apps on the documentation machine or deliberately curate the composition before generating again. Merely renaming a placeholder does not demonstrate working icons.

Output is under `artifacts/readme-render/<run>/`: full dark/light launchers, groups, expanded folders, curated app lists and offline search. The sample date/time is fixed for consistent English clocks. If FFmpeg is available, the script encodes `start-motion.gif` from the native transition frames over a generated background. It never records the real desktop.

The renderer also checks collapsed/expanded glyph geometry across eight languages, two themes and short/tall viewports, and produces the current sidebar, selection-mode and themed-confirmation images. Wait for localized live frames after changing languages; blank loading tiles are not finished documentation. Refresh every full-launcher image and the GIF when navigation or persistent controls move.

## Settings and launch options

`scripts/test-fidelity.ps1` produces English `start-behavior.png`, `start-behavior-layout.png` and `launch-options.png` in its fixture output. These native dialog tests also verify all eight languages and both base themes. Run native interactive fixtures on a dedicated desktop/CI runner; use their artifact images rather than photographing a working user's screen. The launch example uses `%USERPROFILE%\Documents` literally and a fictional command line, not an expanded user path.

## Product video

With FFmpeg and ffprobe on `PATH`, run:

```powershell
./scripts/render-promo.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe -Theme Dark
./scripts/render-promo.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe -Theme Light
```

The separate `--render-promo` entry point uses the same hidden, isolated renderer. It produces a silent 60-second, 1920 × 1080, 30 fps H.264 MP4, a poster, a short GIF preview and review frames under `artifacts/promo/<run>/`. Scenes show the full launcher, its native entrance transition, an actual tile move and undo, inline folder expansion, app search, HTML/CSS/SVG live tiles, four distinct web examples, keyboard placement/locking and Tile Studio. Each run uses the chosen base theme throughout, including the video frame and title cards. Time and aggregate metrics are synthetic. The renderer checks actual move/undo/keyboard/lock results and rejects any visible window.

The wrapper verifies playback format, duration and attachment size with ffprobe, then fully decodes the MP4 with FFmpeg. Inspect decoded frames across every scene as well as the poster and GIF; technical validation alone does not verify composition or privacy. The rearrangement scene keeps the pointer path straight and holds the before/after states. It uses production planners and checks that the group never expands mid-drag and that the displaced neighbor uses the released slot. Placement easing is sampled at synthetic frame times, so encoder speed cannot change the movement curve.

Attach the reviewed MP4 to the release for downloading. For inline README playback, upload the same file as a GitHub media attachment and put its `github.com/user-attachments/assets/…` URL on its own paragraph in all eight READMEs. GitHub CLI 2.99 or newer supports `gh pr edit --attach` for this upload; see the [official attachment guide](https://docs.github.com/en/github-cli/github-cli/attaching-files-with-github-cli). Verify the player on the rendered GitHub page. The Pages site keeps a playback copy in its static assets. Keep generated fixture directories, browser profiles and intermediate movie frames out of Git history. See [third-party components](third-party.md) for the distinction between documentation tools and shipped application dependencies.

## Review before committing

Inspect every selected PNG and representative opening/closing GIF frames. Confirm readable real icons; English labels inside tiles; no username, path, personal filenames, real weather location, taskbar, notification or desktop imagery. Check that file metadata does not include personal source data. Copy **only reviewed PNG/GIF files** into `docs/images`; never copy fixture directories, caches, JSON, logs, bindings or source icon catalogs. Existing live/data/size images also use isolated English synthetic fixtures.

Run `scripts/check-readmes.ps1`, the staged privacy guard and `git diff --check`. The privacy guard scans bytes and private paths/credentials but does **not** replace visual review or detect every word drawn inside an image. All language READMEs share these English images. Translated prose has an AI-translation notice and links back to English.

`test-studio.ps1` also writes native-resolution 720×720 dark/light gallery PNGs from the real bundled HTML/CSS/JavaScript, with offline sample input. These captures retain the packages’ own colors and theme-aware transparent artwork. Use the paired launcher, inline-folder, search, web-example, Studio and gallery assets on Pages. Theme changes update full-resolution links and video posters as well as the video source; a playing video keeps its position, and a paused video stays paused.
