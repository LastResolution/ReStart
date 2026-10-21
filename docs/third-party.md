# Third-party components

ReStart's own source is distributed under the [MIT license](../LICENSE). Dependencies keep their own licenses and copyright notices; they are not relicensed as ReStart code.

## Shipped application dependencies

| Component | Resolved version | License | Included notice |
| --- | --- | --- | --- |
| [Jint](https://github.com/sebastienros/jint/tree/v4.17.0) | 4.17.0 | BSD-2-Clause | [Jint-LICENSE.txt](../third-party/Jint-LICENSE.txt) |
| [Acornima](https://github.com/adams85/acornima/tree/v1.7.0), through Jint | 1.7.0 | BSD-3-Clause | [Acornima-LICENSE.txt](../third-party/Acornima-LICENSE.txt) and [Acornima-NOTICE.txt](../third-party/Acornima-NOTICE.txt), including its Esprima.NET notice |
| .NET runtime and Windows Desktop runtime | Selected by the installed stable .NET SDK at publish time | MIT, with bundled third-party notices | `third-party/DotNet-Runtime-LICENSE.txt`, `DotNet-Runtime-NOTICES.txt` and `DotNet-Desktop-LICENSE.txt` in each portable ZIP |

The worker's [lock file](../src/ReStart.TileHost/packages.lock.json) records the complete application NuGet dependency graph for its current target. These permissive dependencies do not require changing ReStart's MIT license. Keep their full notices when redistributing the application.

[`scripts/package.ps1`](../scripts/package.ps1) copies the repository notices and the license/notice files from the **exact runtime packs resolved for that publish** into both x64 and ARM64 packages. The application also exposes bundled notices under **Settings → About → Licenses**. When updating a dependency, review its pinned source license, upstream notices and resolved transitive graph before committing the lock-file change. Review any newly shipped runtime-pack notices too.

## Development tools and documentation

Test dependencies such as xUnit, Microsoft.NET.Test.Sdk and coverlet are used for verification and are not copied into portable packages. GitHub Actions and local documentation tools are also separate from the shipped application.

FFmpeg/ffprobe are optional, separately installed tools for generating documentation animations and the product video. ReStart does not redistribute their executables or link their libraries. Review the [FFmpeg licensing information](https://ffmpeg.org/legal.html) before changing that arrangement; enabling GPL components changes the license of an FFmpeg build.

App names and icons in documentation illustrate installed applications. Their trademarks remain with their respective owners; those app binaries, icon catalogs and Windows fonts are not bundled with ReStart. The launcher resolves installed app icons and system fonts on the user's machine. ReStart is an independent project and is not affiliated with Microsoft or the illustrated applications.

## Connected services and user content

Remote data services and imported tile packages have their own terms. The weather sample displays Open-Meteo attribution; its public free endpoint has non-commercial service conditions. See [the weather configuration and service links](javascript-tiles.md#weather-location). ReStart's MIT license does not grant rights to third-party feeds, images, APIs or user-provided scripts.
