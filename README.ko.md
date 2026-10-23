<p align="center"><img src="assets/ReStart.png" width="96" alt="ReStart" /></p>
<h1 align="center">ReStart</h1>
<p align="center"><strong>전체 화면 시작 메뉴를 다시 만나세요.</strong><br>내 앱, 내 배치, 직접 만드는 라이브 타일.</p>

<!-- languages:start -->
<p align="center">
  <a href="README.md">English</a> · <a href="README.de.md">Deutsch</a> · <a href="README.es.md">Español</a> · <a href="README.fr.md">Français</a> · <a href="README.ja.md">日本語</a> · <a href="README.ko.md">한국어</a> · <a href="README.zh-CN.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a>
</p>
<!-- languages:end -->

<p align="center"><a href="https://lastresolution.github.io/ReStart/">웹사이트(영어)</a> · <a href="https://lastresolution.github.io/ReStart/developers/">개발자 가이드(영어)</a></p>

> **AI 번역:** 이 안내서는 영어를 바탕으로 AI로 번역했으며 오류가 있을 수 있습니다. [영어 README](README.md)가 기준 문서이며 추가 예제를 제공합니다. 번역 수정 제안을 환영합니다. 스크린샷은 영어 데모와 예시 데이터를 사용합니다.

<p align="center"><strong>60초 데모 보기</strong><br><sub>1080p · 영어 · 소리 없음</sub></p>

https://github.com/user-attachments/assets/243a5c5a-46bc-4180-8a0c-cee71ab63f06

Windows 10의 전체 화면 시작 메뉴가 그리우신가요? ReStart는 Windows 11과 Windows 10에서 익숙한 앱 공간을 제공합니다. 자주 쓰는 항목을 고정하고 원하는 대로 배치하며 JavaScript로 작성할 수 있는 라이브 타일을 추가하세요.

**포터블 · JavaScript 라이브 타일 · x64 및 ARM64 · 8개 언어 · 별도 .NET 설치 불필요**

## 더 자유로운 배치와 타일 제작

전체 배치 또는 개별 그룹을 잠글 수 있습니다. Ctrl+Shift+L로 전체 잠금을 전환합니다. Ctrl+E로 선택하고 Ctrl+방향키로 타일, Ctrl+Shift+방향키로 그룹을 이동하며 Alt+1–4로 크기를 바꿉니다. Ctrl+G는 그룹화, Shift+F10은 메뉴 열기입니다. 실행 취소/다시 실행도 지원합니다. 바탕 화면과 별도의 배경 이미지를 저장할 수 있습니다.

**설정 → 모양**에서 배경 농도를 **10–100%**, 배경 이미지 흐림을 **0–50**으로 조절할 수 있습니다. 흐림 기본값은 **12**이며 **0**은 이미지를 선명하게 표시합니다. 고대비 설정이 우선합니다. **선택 마치기** 또는 **고정된 타일**을 누르면 타일 화면으로 바로 돌아갑니다.

모양, 타일 배율, 그룹 너비와 언어를 저장 전에 미리 볼 수 있습니다. 변경 후 닫을 때 저장, 버리기 또는 계속 편집을 선택하며 버리면 저장된 표시로 돌아갑니다. 직접 요청한 업데이트 다운로드가 끝나면 재시작 알림이 유지됩니다. 검증된 준비 상태는 다음 실행에서도 복원되며 자동 설치하지 않습니다.

HTML/CSS/JavaScript 타일 19개를 제공합니다. Tile Studio에서 코드와 매니페스트를 편집하고 오프라인 샘플로 네 가지 크기를 확인한 뒤 저장하거나 고정할 수 있습니다. 날씨 풍경, 시스템 대시보드, RSS 뉴스 보드와 Canvas 정원을 추가했습니다. SVG 시계 바늘은 PC 시간을 따릅니다. 타일 이름은 기본으로 표시되며 설정에서 숨길 수 있습니다.

드래그 중에는 타일, 그룹 또는 여러 선택 항목의 반투명 복사본이 포인터를 따라갑니다. 파란 표시로 이동 위치를 안내하고 밀려나는 타일도 놓기 전에 새 위치를 보여 줍니다.

![Tile Studio: HTML, CSS and JavaScript with four live previews](docs/images/tile-studio.png)

![Weather scene, System dashboard, News board and Canvas garden](docs/images/web-examples.png)

**편집기 조작:** JavaScript, HTML, CSS, JSON 구문 강조; <kbd>Tab</kbd> / <kbd>Shift</kbd> + <kbd>Tab</kbd> 들여쓰기; **Format document / Ctrl+Shift+F** 오프라인 서식 정리를 지원하며 한 번에 실행 취소할 수 있습니다. Feed headlines와 News board는 타일 안에서 스크롤됩니다. 닫힌 폴더는 같은 크기의 라이브 미리보기를 표시합니다. 초기 및 초기화 배치는 Month dots와 Orbit clock을 사용합니다. 선택적인 시작 시 업데이트 확인은 새 버전을 닫을 수 있는 알림으로 안내합니다.

## 시작하기

1. Intel/AMD PC용 **[x64 ZIP](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-x64.zip)** 또는 Windows on ARM용 **[ARM64 ZIP](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-arm64.zip)**을 받습니다.
2. **ZIP 전체를** 압축 해제합니다. `TileHost`, `Examples`, `third-party`를 `ReStart.exe` 옆에 유지합니다.
3. **ReStart.exe**를 실행하고 **Windows 키**를 누른 다음 앱 이름을 입력합니다. <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>Space</kbd>와 트레이 메뉴로도 열 수 있습니다.

톱니바퀴에서 테마, 로그인 시 시작, Windows 키/시작 버튼 연동, 업데이트를 설정합니다. 일반 사용에는 관리자 권한이 필요 없습니다. [SHA-256 체크섬](https://github.com/LastResolution/ReStart/releases/latest)을 제공합니다.

## 기능

![ReStart 열기 및 닫기](docs/images/start-motion.gif)

**기본은 그룹, 필요할 때 자유 배치.** 빈 공간에 놓으면 이름 없는 그룹을 만들고, 타일 위에서 기다리면 폴더를 만듭니다. <kbd>Shift</kbd>를 누른 채 드래그하면 폴더 없이 재배치하고, <kbd>Alt</kbd>를 누르면 빈 공간에 독립 배치합니다. Settings → Start behavior의 **Use free tile placement**로 독립 배치를 기본값으로 선택할 수 있습니다. 기존 배치와 폴더는 유지됩니다. 포인터 아래 대상을 바꾸지 않고 주변 타일이 부드럽게 이동합니다.


| 영역 | 제공 기능 |
| --- | --- |
| 열기 및 닫기 | 선택적 Windows 키/작업 표시줄 시작 버튼 가로채기, Ctrl+Alt+Space, 트레이, 전경 활성화, 앱/작업 표시줄 전환 시 닫기, 모니터별 작업 영역, 단일 인스턴스 |
| 시작 | 선택적 로그인 시 시작, 백그라운드 모드, 테마가 적용된 준비 화면과 미리 입력, 아이콘 캐시, 앱 목록 새로 고침 |
| 타일 및 그룹 | 4가지 크기, 독립 타일, 내용에 맞는 직사각형 이동 그룹, 가로 반 셀 기준점, 자동 배치와 양방향 스크롤 |
| 폴더 | 겹쳐서 기다리면 만들기, 배치 안에서 펼치기, 항목 추가/제거/정렬, 인라인 이름 변경 및 풀기; 항목이 하나 남아도 폴더 유지 |
| 배치 편집 | 편집 모드, 다중 선택, 일괄 이동, 세션 실행 취소/다시 실행; 드래그 취소 시 원래 배치 유지 |
| 드래그 안내 | 그룹/폴더 대상 구분, 대기 진행률, 선택적 힌트와 그룹 경계, 가장자리 자동 스크롤; 자동 그룹/폴더 생성 끄기 |
| 고정 | 앱, 파일, 디렉터리, 바로 가기, 웹사이트; Explorer **Pin to ReStart**, 우클릭 메뉴, 앱 목록에서 드래그, 명시적 대상 지정 |
| 검색 | 정규화된 앱 이름, 영어 별칭을 포함한 번역된 Windows 설정 15개, 선택적 로컬 인덱스 파일 이름 검색, 방향키/Enter, 오래된 검색 결과 무시 |
| 모든 앱 | 반응형 알파벳 열, 시작 메뉴 폴더 펼치기, 문자 선택, 키보드 탐색, 설치/제거 반영, 최근 추가 및 선택적 자주 사용하는 앱 |
| 앱 명령 | 사용 가능한 Windows 관리자 실행/파일 위치/속성/제거, Windows 기록 설정과 API 지원에 따른 최근/자주 사용하는 문서 메뉴 |
| 실행 옵션 | 타일별 켜기/끄기, 인수, 작업 디렉터리, 지원되는 데스크톱 앱/바로 가기의 관리자 실행; UAC는 Windows가 처리 |
| 탐색 | 선택적 마우스 올리기 확장, 문서/다운로드/음악/사진/동영상/네트워크/개인 폴더 개별 선택, 계정 및 전원 작업 |
| 모양 | 밝음/어두움 변형이 있는 5개 색상 계열, 시스템 모드, 사용자 RGB 배경/강조색, 바탕 화면 배경, 시작 화면 색조, 개별 타일 색상, 테마 컨트롤/스크롤바, 고대비, 선택적 애니메이션 |
| 라이브 타일 | JavaScript 예제 19개, 반응형 크기 4개, 로컬 클릭 동작, 실시간 갱신 켜기/끄기, 데이터 구성, 예제 생성, 패키지 가져오기, 자동 감시 라이브러리 |
| 타일 API | PC 시간, 언어/크기, 유지되는 상태, CPU/메모리/네트워크/배터리/저장 공간, 선택한 사진, 캐시된 JSON/Web API/RSS/Atom/파일 입력, 로컬 앱 연동 |
| 언어 | 영어, 일본어, 독일어, 프랑스어, 스페인어, 한국어, 중국어 간체/번체; 기본 제공 타일 문구 포함 |
| 데이터 및 업데이트 | 로컬 설정, 배치 가져오기/내보내기/초기화, 백업과 마이그레이션, 지울 수 있는 앱 활동, 버전/빌드/라이선스, API 키 없는 확인, 검증 및 복구가 가능한 포터블 업데이트, 진단 보고서 미리 보기 |
| 배포 | 런타임/고지문을 포함한 Windows 10/11 x64 및 ARM64 포터블 패키지, Explorer 패치 없는 되돌릴 수 있는 연동 |

[기능 스위치와 기본값(영어)](docs/start-behavior.md) · [호환성(영어)](docs/fidelity.md)

**실용적인 초기 배치.** 첫 실행과 배치 초기화 시 앱 ID와 정확한 번역 이름을 사용해 설치된 앱을 용도별로 선택합니다. 앱이 적으면 중복되지 않는 기본 오프라인 타일로 그룹마다 최소 6개를 채웁니다. 저장된 배치는 유지됩니다. [선택 및 초기화 동작(영어)](docs/start-behavior.md#initial-layout-and-reset).

**측정된 플랫폼 검증 범위.** 네이티브 검사는 100%/125%/150% 배율의 화면 2개와 세로·가로 혼합 구성에서 창의 위치와 크기를 확인합니다. 접근성 공급자는 8개 언어·두 테마에서 검사하며, Narrator의 기본 안내는 음성 요약으로 확인했습니다. 화면 재연결, 실제 터치 입력, 화면 읽기 프로그램의 전체 조작 흐름은 추가 기기 검사가 필요합니다. [결과 및 기기 범위(영어)](docs/fidelity.md#observed-platform-coverage).

## 검색 및 구성

![앱, Windows 설정, 로컬 파일 검색 결과](docs/images/unified-search.png)

**Settings → Start behavior**에서 설정/파일 검색, 문서 메뉴, 마우스 올리기 효과, 자동 그룹/폴더, 가장자리 스크롤, 드래그 힌트를 각각 끌 수 있습니다. 자주 사용하는 앱 기록은 기본적으로 꺼져 있습니다.

| 시작 동작 | 타일별 실행 옵션 |
| --- | --- |
| ![동작 설정](docs/images/start-behavior.png) | ![인수, 작업 디렉터리, 관리자 실행](docs/images/launch-options.png) |

**Use custom launch options**를 꺼도 입력한 값은 유지됩니다. 이 옵션을 사용하지 않으면 바로 가기는 기본 Windows 실행 설정을 따릅니다.

## 라이브 타일과 데이터

![기본 제공 라이브 타일](docs/images/live-tiles.png)

- **시간과 집중:** Orbit clock, Digital clock, Month dots, Focus cycle, Year in motion, Countdown.
- **PC:** CPU pulse, Memory flow, Network flow, Battery, Storage.
- **사진:** 직접 고른 폴더에서 표시하고 클릭으로 다음 사진을 여는 Photo moments.
- **데이터 연결:** Weather, Feed headlines, JSON monitor.

배경 우클릭 → **Add live tile**. 클릭은 타이머 일시 중지나 다음 사진 같은 타일 내부 동작을 수행합니다. Weather/Feed/JSON은 **Configure data sources…**에서 URL이나 파일을 지정하고 저장합니다. **Refresh data now**로 새 값을 요청할 수 있습니다.

**날씨의 초기 좌표는 예시용 베를린이며 PC 위치가 아닙니다.** ReStart는 지역을 추정하지 않습니다. [날씨 안내](docs/javascript-tiles.md#weather-location)에 따라 좌표와 표시 이름을 바꾸세요. 자격 증명은 현재 Windows 사용자용으로 암호화하며 내보내기에서 제외합니다. 연결 실패 시 캐시를 사용할 수 있고 시작 화면을 숨기면 읽기를 중지합니다.

웹 타일은 manifest.json, index.html, tile.css, tile.js와 선택적 assets로 구성됩니다. 네 가지 고정 크기 안에서 HTML/CSS, SVG, Canvas와 투명 배경을 사용할 수 있습니다. 기존 네이티브 JS도 지원합니다. WebView2 Runtime이 필요하며 직접 네트워크 접근에는 선언과 타일별 허용이 필요합니다. 일반 파일, CLR, 셸 브리지는 제공하지 않습니다. [HTML/CSS/JavaScript API](docs/javascript-tiles.md) · [Examples](examples/tiles)

## 조작

| 동작 | 결과 |
| --- | --- |
| Windows 키 / <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>Space</kbd> | 열기 또는 닫기 |
| 입력 / <kbd>Ctrl</kbd> + <kbd>F</kbd> | 검색; Enter는 강조된 결과를 실행하고 방향키는 선택 변경 |
| <kbd>Esc</kbd> | 검색, 폴더, 오버레이를 닫은 다음 시작 화면 닫기 |
| 타일 / 그룹 제목 드래그 | 이동; 빈 공간에 놓으면 이름 없는 그룹 생성 |
| <kbd>Alt</kbd> + 빈 공간으로 드래그 | 이번 드래그만 독립 배치 |
| <kbd>Shift</kbd> + 드래그 | 겹쳐도 그룹이나 폴더 동작 없이 주변 타일을 밀어 재배치 |
| 타일에 약 0.6초 겹치기 | 미리 보기 후 폴더 생성; 기존 폴더에는 항목 추가 |
| 사이드바/배경 메뉴의 **타일 선택** / <kbd>Ctrl</kbd> + <kbd>E</kbd> | 앱을 열지 않고 여러 타일 선택. 평소에도 타일을 바로 끌어 이동할 수 있습니다 |
| 편집 중 <kbd>Ctrl</kbd> + <kbd>A</kbd> / <kbd>Delete</kbd> | 루트 타일 전체 선택 / 선택 고정 해제 |
| <kbd>Ctrl</kbd> + <kbd>Z</kbd> / <kbd>Ctrl</kbd> + <kbd>Y</kbd> 또는 <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Z</kbd> | 배치 실행 취소 / 다시 실행 |
| 연필 / 이름 더블클릭 | 그룹 또는 폴더 이름 인라인 변경 |
| <kbd>Shift</kbd> + 휠 | 가로 스크롤 |

Windows 조합 키는 운영체제로 전달합니다. 배치 기록은 세션 중 최대 100회/32 MiB이며 텍스트 필드는 자체 실행 취소를 사용합니다. 다시 열면 고정 타일 화면으로 돌아옵니다.

## 설정, 개인정보 및 업데이트

설정은 `%LOCALAPPDATA%\ReStart\layout.json`, 선택적 앱 활동은 별도 `activity.json`에 저장하며 지울 수 있습니다. 마이그레이션은 백업을 보관합니다. [세부 정보(영어)](docs/migration.md). About에서 버전, 빌드, 라이선스를 확인합니다.

공개 GitHub Releases를 API 키 없이 확인합니다. 설정 → 업데이트에서 **업데이트 다운로드**, 검증 후 **다시 시작하여 업데이트**를 선택하세요. 쓰기 가능한 폴더의 완전하고 수정되지 않은 포터블 패키지는 설치 또는 시작 실패 시 이전 파일로 복원합니다. 개발 빌드와 이전 패키지는 수동으로 교체할 수 있습니다. 정보 → 진단 정보에서 경로, 앱 목록, 타일 데이터, 자격 증명 및 원본 로그를 포함하지 않는 보고서를 확인하고 복사할 수 있습니다. 자동으로 업로드하지 않습니다. [업데이트 및 진단(영어)](docs/updates.md). 위치가 바뀌면 시작 등록과 Explorer 연동을 다시 저장합니다. 광고나 사용 분석이 없고 앱 목록, 배치, 사진 선택은 업로드하지 않습니다. 구성한 데이터 제공자는 해당 요청을 받습니다. [보안(영어)](SECURITY.md)

제거하려면 시작 등록과 Explorer 연동을 끄고 트레이에서 종료한 뒤 설치 폴더를 삭제합니다. 설정과 사용자 타일까지 지우려는 경우에만 로컬 데이터도 삭제하세요.

## 직접 빌드

Windows와 `global.json`에 지정된 안정 버전 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)를 사용합니다. 파일을 교체하기 전에 실행 중인 앱을 종료하세요.

```powershell
git clone https://github.com/LastResolution/ReStart.git
cd ReStart
dotnet restore ReStart.slnx --locked-mode
dotnet build ReStart.slnx -c Release --no-restore
dotnet test ReStart.slnx -c Release --no-build
.\src\ReStart.App\bin\Release\net10.0-windows\ReStart.exe
# 격리된 미리 보기:
dotnet run --project src/ReStart.App -c Release -- --preview
# 런타임을 포함한 포터블 패키지:
./scripts/package.ps1 -Runtime win-x64
```

ARM64는 `win-arm64`를 사용합니다. ZIP과 체크섬은 `artifacts/packages`에 생성합니다. 항상 전체 ZIP을 배포하세요.

[질문과 작품 공유](https://github.com/LastResolution/ReStart/discussions) · [버그 신고](https://github.com/LastResolution/ReStart/issues/new/choose) · [기여(영어)](CONTRIBUTING.md) · [아키텍처(영어)](docs/architecture.md) · [테스트(영어)](docs/testing.md) · [릴리스](https://github.com/LastResolution/ReStart/releases)

ReStart는 후기 Windows 10 전체 화면 시작 메뉴에서 영감을 받은 독립 MIT 프로젝트이며 Microsoft와 제휴하지 않습니다. 이미지는 영어 데모, 합성 메트릭, 생성한 사진 색상을 사용하는 실제 앱입니다. 이름과 아이콘은 각 권리자에게 속합니다. [MIT 라이선스](LICENSE) · [타사 라이선스 안내(영어)](docs/third-party.md) · [지원(영어)](SUPPORT.md)

---

<!-- support:start -->
<h2 align="center">개인 개발 응원하기</h2>
<p align="center">
  ReStart가 마음에 드시나요? LastResolution이 ReStart와 앞으로의 유용한 도구를 만들고 개선할 수 있도록 응원해 주세요.<br>
  <sub>ReStart는 무료 오픈 소스 소프트웨어입니다. 후원은 언제나 선택 사항입니다.</sub>
</p>
<p align="center">
  <a href="https://buymeacoffee.com/LastResolution"><img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" height="48" alt="Buy Me a Coffee · LastResolution" /></a>
</p>
<!-- support:end -->
