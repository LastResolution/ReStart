# Documentation images

[日本語](ja/screenshots.md)

README screenshots use **English across the whole image**, including app/tile names, live-tile titles, weekdays, dates, settings and example documents. They use curated common applications, fictional files, synthetic aggregate metrics and generated photo colors. Do not capture a personal desktop or export a user's installed-app list into documentation.

## Full launcher and animation

Build a Release executable, then run:

```powershell
./scripts/render-readme.ps1 -Executable src/ReStart.App/bin/Release/net10.0-windows/ReStart.exe
```

The dedicated `--render-docs` entry point returns before normal instance management, tray, hooks, pin IPC and startup registration. It never shows a window. It uses a fresh ignored data directory, renders WPF controls directly, and stops its workers on exit. Real settings, wallpaper, chosen photos, data connections and document history are not used.

The renderer obtains actual icons for its fixed app selection from the local Shell and matches package/executable identities independently of localized names. It fails if a required application icon cannot be resolved; do not publish fallback website/globe/list icons under another application's name. Install the selected common apps on the documentation machine or deliberately curate the composition before generating again. Merely renaming a placeholder does not demonstrate working icons.

Output is under `artifacts/readme-render/<run>/`: full dark/light launchers, groups, expanded folders, curated app lists and offline search. The sample date/time is fixed for consistent English clocks. If FFmpeg is available, the script encodes `start-motion.gif` from the native transition frames over a generated background. It never records the real desktop.

## Settings and launch options

`scripts/test-fidelity.ps1` produces English `start-behavior.png`, `start-behavior-layout.png` and `launch-options.png` in its fixture output. These native dialog tests also verify all eight languages and both base themes. Run native interactive fixtures on a dedicated desktop/CI runner; use their artifact images rather than photographing a working user's screen. The launch example uses `%USERPROFILE%\Documents` literally and a fictional command line, not an expanded user path.

## Review before committing

Inspect every selected PNG and representative opening/closing GIF frames. Confirm readable real icons; English labels inside tiles; no username, path, personal filenames, real weather location, taskbar, notification or desktop imagery. Check that file metadata does not include personal source data. Copy **only reviewed PNG/GIF files** into `docs/images`; never copy fixture directories, caches, JSON, logs, bindings or source icon catalogs. Existing live/data/size images also use isolated English synthetic fixtures.

Run `scripts/check-readmes.ps1`, the staged privacy guard and `git diff --check`. The privacy guard scans bytes and private paths/credentials but does **not** replace visual review or detect every word drawn inside an image. All language READMEs share these English images. Translated prose has an AI-translation notice and links back to English.
