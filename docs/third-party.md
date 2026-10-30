# Third-party components

ReStart's own source is distributed under the [MIT license](../LICENSE). Dependencies keep their own licenses and copyright notices; they are not relicensed as ReStart code.

## Shipped application dependencies

| Component | Resolved version | License | Included notice |
| --- | --- | --- | --- |
| [Prettier](https://prettier.io/docs/browser) standalone and bundled Babel, ESTree, HTML and PostCSS parsers | 3.9.10 | MIT and the third-party notices in its distributed license | [Prettier-LICENSE.txt](../third-party/Prettier-LICENSE.txt); exact npm integrity and SHA-256 file hashes in [the formatter asset lock](../src/ReStart.App/Assets/Formatter/package-lock.json) |
| [AvalonEdit](https://github.com/icsharpcode/AvalonEdit/tree/862415d51eddc9eac93f462dbc522ffbf929cd52) | 6.3.1.120 | MIT | [AvalonEdit-LICENSE.txt](../third-party/AvalonEdit-LICENSE.txt) from the resolved package's source commit |
| [Jint](https://github.com/sebastienros/jint/tree/v4.17.0) | 4.17.0 | BSD-2-Clause | [Jint-LICENSE.txt](../third-party/Jint-LICENSE.txt) |
| [Acornima](https://github.com/adams85/acornima/tree/v1.7.0), through Jint | 1.7.0 | BSD-3-Clause | [Acornima-LICENSE.txt](../third-party/Acornima-LICENSE.txt) and [Acornima-NOTICE.txt](../third-party/Acornima-NOTICE.txt), including its Esprima.NET notice |
| [Microsoft.Web.WebView2](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4258.31) SDK | 1.0.4258.31 | BSD-3-Clause | [WebView2-LICENSE.txt](../third-party/WebView2-LICENSE.txt) and [WebView2-NOTICE.txt](../third-party/WebView2-NOTICE.txt) from the resolved SDK package |
| Microsoft.Windows.SDK.NET.Ref runtime projections | 10.0.17763.57 | Microsoft Windows SDK license; C#/WinRT includes MIT-licensed code | [WindowsSDK-LICENSE.txt](../third-party/WindowsSDK-LICENSE.txt) from the package's official license URL and [CsWinRT-LICENSE.txt](../third-party/CsWinRT-LICENSE.txt) |
| .NET runtime and Windows Desktop runtime | Selected by the installed stable .NET SDK at publish time | MIT, with bundled third-party notices | `third-party/DotNet-Runtime-LICENSE.txt`, `DotNet-Runtime-NOTICES.txt` and `DotNet-Desktop-LICENSE.txt` in each portable ZIP |

The app and worker [lock files](../src/ReStart.App/packages.lock.json) record their NuGet package graphs. The stable Windows target also resolves the SDK targeting pack through the .NET framework reference. ReStart's own code keeps its MIT license; dependencies retain their licenses. Keep the full notices when redistributing the application. The WebView2 browser runtime is a separately installed Microsoft component, not bundled in the ZIP; the application reports when it is missing.

[`scripts/package.ps1`](../scripts/package.ps1) copies the repository notices and the license/notice files from the **exact runtime packs resolved for that publish** into both x64 and ARM64 packages. The application also exposes bundled notices under **Settings → About → Licenses**. When updating a dependency, review its pinned source license, upstream notices and resolved transitive graph before committing the lock-file change. Review any newly shipped runtime-pack notices too.

The independently implemented native Jump List adapter references Open-Shell’s MIT-licensed interop contracts at pinned revision `9dfa7fa5c3614de15d0ff4f3eba7edd9e2dcbd24`. [Attribution and the source license](../third-party/Open-Shell-interoperability.txt) are bundled in both packages and the Licenses view. No Open-Shell binaries, images or other project assets are included.

## Development tools and documentation

Test dependencies such as xUnit, Microsoft.NET.Test.Sdk and coverlet are used for verification and are not copied into portable packages. GitHub Actions and local documentation tools are also separate from the shipped application.

FFmpeg/ffprobe are optional, separately installed tools for generating documentation animations and the product video. ReStart does not redistribute their executables or link their libraries. Review the [FFmpeg licensing information](https://ffmpeg.org/legal.html) before changing that arrangement; enabling GPL components changes the license of an FFmpeg build.

App names and icons in documentation illustrate installed applications. Their trademarks remain with their respective owners; those app binaries, icon catalogs and Windows fonts are not bundled with ReStart. The launcher resolves installed app icons and system fonts on the user's machine. ReStart is an independent project and is not affiliated with Microsoft or the illustrated applications.

## Connected services and user content

Remote data services and imported tile packages have their own terms. The weather sample displays Open-Meteo attribution; its public free endpoint has non-commercial service conditions. See [the weather configuration and service links](javascript-tiles.md#weather-location). ReStart's MIT license does not grant rights to third-party feeds, images, APIs or user-provided scripts.
