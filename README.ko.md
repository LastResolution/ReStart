<p align="center"><img src="assets/ReStart.png" width="96" alt="ReStart" /></p>
<h1 align="center">ReStart</h1>
<p align="center"><strong>전체 화면 시작 메뉴를 다시 만나세요.</strong><br>내 앱, 내 배치, 직접 만드는 라이브 타일.</p>

<!-- languages:start -->
<p align="center">
  <a href="README.md">English</a> · <a href="README.de.md">Deutsch</a> · <a href="README.es.md">Español</a> · <a href="README.fr.md">Français</a> · <a href="README.ja.md">日本語</a> · <a href="README.ko.md">한국어</a> · <a href="README.zh-CN.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a>
</p>
<!-- languages:end -->

> **AI 번역:** 이 안내서는 영어를 바탕으로 AI로 번역했으며 오류가 있을 수 있습니다. [영어 README](README.md)가 기준 문서이며 추가 예제를 제공합니다. 번역 수정 제안을 환영합니다. 스크린샷은 영어 데모와 예시 데이터를 사용합니다.

![앱, 폴더, 라이브 타일이 있는 ReStart](docs/images/start.png)

Windows 10의 전체 화면 시작 메뉴가 그리우신가요? ReStart는 Windows 11과 Windows 10에서 익숙한 앱 공간을 제공합니다. 자주 쓰는 항목을 고정하고 원하는 대로 배치하며 JavaScript로 작성할 수 있는 라이브 타일을 추가하세요.

**포터블 · JavaScript 라이브 타일 · x64 및 ARM64 · 8개 언어 · 별도 .NET 설치 불필요**

## 시작하기

1. Intel/AMD PC용 **[x64 ZIP](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-x64.zip)** 또는 Windows on ARM용 **[ARM64 ZIP](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-arm64.zip)**을 받습니다.
2. **ZIP 전체를** 압축 해제합니다. `TileHost`, `Examples`, `third-party`를 `ReStart.exe` 옆에 유지합니다.
3. **ReStart.exe**를 실행하고 **Windows 키**를 누른 다음 앱 이름을 입력합니다. **Ctrl+Alt+Space**와 트레이 메뉴로도 열 수 있습니다.

톱니바퀴에서 테마, 로그인 시 시작, Windows 키/시작 버튼 연동, 업데이트를 설정합니다. 일반 사용에는 관리자 권한이 필요 없습니다. [SHA-256 체크섬](https://github.com/LastResolution/ReStart/releases/latest)을 제공합니다.

## 기능

![ReStart 열기 및 닫기](docs/images/start-motion.gif)

| 영역 | 제공 기능 |
| --- | --- |
| 열기 및 닫기 | 선택적 Windows 키/작업 표시줄 시작 버튼 가로채기, Ctrl+Alt+Space, 트레이, 전경 활성화, 앱/작업 표시줄 전환 시 닫기, 모니터별 작업 영역, 단일 인스턴스 |
| 시작 | 선택적 로그인 시 시작, 백그라운드 모드, 테마가 적용된 준비 화면과 미리 입력, 아이콘 캐시, 앱 목록 새로 고침 |
| 타일 및 그룹 | 4가지 크기, 독립 타일, 내용에 맞는 직사각형 이동 그룹, 가로 반 셀 기준점, 자동 배치와 양방향 스크롤 |
| 폴더 | 그룹 안에서 겹쳐 만들기, 배치 안에서 펼치기, 항목 추가/제거/정렬, 인라인 이름 변경 및 풀기; 항목이 하나 남아도 폴더 유지 |
| 배치 편집 | 편집 모드, 다중 선택, 일괄 이동, 세션 실행 취소/다시 실행; 드래그 취소 시 원래 배치 유지 |
| 드래그 안내 | 그룹/폴더 대상 구분, 대기 진행률, 선택적 힌트와 그룹 경계, 가장자리 자동 스크롤; 자동 그룹/폴더 생성 끄기 |
| 고정 | 앱, 파일, 디렉터리, 바로 가기, 웹사이트; Explorer **Pin to ReStart**, 우클릭 메뉴, 앱 목록에서 드래그, 명시적 대상 지정 |
| 검색 | 정규화된 앱 이름, 영어 별칭을 포함한 번역된 Windows 설정 15개, 선택적 로컬 인덱스 파일 이름 검색, 방향키/Enter, 오래된 검색 결과 무시 |
| 모든 앱 | 반응형 알파벳 열, 시작 메뉴 폴더 펼치기, 문자 선택, 키보드 탐색, 설치/제거 반영, 최근 추가 및 선택적 자주 사용하는 앱 |
| 앱 명령 | 사용 가능한 Windows 관리자 실행/파일 위치/속성/제거, Windows 기록 설정과 API 지원에 따른 최근/자주 사용하는 문서 메뉴 |
| 실행 옵션 | 타일별 켜기/끄기, 인수, 작업 디렉터리, 지원되는 데스크톱 앱/바로 가기의 관리자 실행; UAC는 Windows가 처리 |
| 탐색 | 선택적 마우스 올리기 확장, 문서/다운로드/음악/사진/동영상/네트워크/개인 폴더 개별 선택, 계정 및 전원 작업 |
| 모양 | 밝음/어두움 변형이 있는 5개 색상 계열, 시스템 모드, 사용자 RGB 배경/강조색, 바탕 화면 배경, 시작 화면 색조, 개별 타일 색상, 테마 컨트롤/스크롤바, 고대비, 선택적 애니메이션 |
| 라이브 타일 | JavaScript 예제 15개, 반응형 크기 4개, 로컬 클릭 동작, 실시간 갱신 켜기/끄기, 데이터 구성, 예제 생성, 패키지 가져오기, 자동 감시 라이브러리 |
| 타일 API | PC 시간, 언어/크기, 유지되는 상태, CPU/메모리/네트워크/배터리/저장 공간, 선택한 사진, 캐시된 JSON/Web API/RSS/Atom/파일 입력, 로컬 앱 연동 |
| 언어 | 영어, 일본어, 독일어, 프랑스어, 스페인어, 한국어, 중국어 간체/번체; 기본 제공 타일 문구 포함 |
| 데이터 및 업데이트 | 로컬 설정, 배치 가져오기/내보내기/초기화, 백업과 마이그레이션, 지울 수 있는 앱 활동, 버전/빌드/라이선스, API 키 없는 업데이트 확인과 수동 설치 |
| 배포 | 런타임/고지문을 포함한 Windows 10/11 x64 및 ARM64 포터블 패키지, Explorer 패치 없는 되돌릴 수 있는 연동 |

[기능 스위치와 기본값(영어)](docs/start-behavior.md) · [호환성(영어)](docs/fidelity.md)

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

자체 타일은 `manifest.json`과 `tile.js`로 구성됩니다. `render(ctx)`는 제한된 데이터 스냅샷을 받아 크기별 배치 등 네이티브 표시 데이터를 반환합니다. JavaScript에 일반 파일/네트워크/CLR 접근 권한은 주어지지 않습니다. `%LOCALAPPDATA%\ReStart\TileLibrary` 아래에 패키지 폴더를 놓으면 자동 인식합니다. [예제](examples/tiles) · [JavaScript API(영어)](docs/javascript-tiles.md) · [로컬 데이터 생성 예제](examples/integrations/write-tile-data.ps1)

## 조작

| 동작 | 결과 |
| --- | --- |
| Windows 키 / `Ctrl+Alt+Space` | 열기 또는 닫기 |
| 입력 / `Ctrl+F` | 검색; Enter는 강조된 결과를 실행하고 방향키는 선택 변경 |
| `Esc` | 검색, 폴더, 오버레이를 닫은 다음 시작 화면 닫기 |
| 타일 / 그룹 제목 드래그 | 이동; 빈 공간에 놓으면 독립 타일 생성 |
| 독립 타일에 약 0.3초 겹치기 | 미리 보기 준비 후 일반 그룹 생성 |
| 그룹 내 타일에 약 0.6초 겹치기 | 폴더 생성; 기존 폴더에는 항목 추가 |
| 사이드바/배경 메뉴의 **타일 선택** / `Ctrl+E` | 앱을 열지 않고 여러 타일 선택. 평소에도 타일을 바로 끌어 이동할 수 있습니다 |
| 편집 중 `Ctrl+A` / `Delete` | 루트 타일 전체 선택 / 선택 고정 해제 |
| `Ctrl+Z` / `Ctrl+Y` 또는 `Ctrl+Shift+Z` | 배치 실행 취소 / 다시 실행 |
| 연필 / 이름 더블클릭 | 그룹 또는 폴더 이름 인라인 변경 |
| `Shift` + 휠 | 가로 스크롤 |

Windows 조합 키는 운영체제로 전달합니다. 배치 기록은 세션 중 최대 100회/32 MiB이며 텍스트 필드는 자체 실행 취소를 사용합니다. 다시 열면 고정 타일 화면으로 돌아옵니다.

## 설정, 개인정보 및 업데이트

설정은 `%LOCALAPPDATA%\ReStart\layout.json`, 선택적 앱 활동은 별도 `activity.json`에 저장하며 지울 수 있습니다. 마이그레이션은 백업을 보관합니다. [세부 정보(영어)](docs/migration.md). About에서 버전, 빌드, 라이선스를 확인합니다.

공개 GitHub Releases를 API 키 없이 확인합니다. ReStart를 종료한 뒤 압축 해제한 설치 전체를 수동 교체하세요. 위치가 바뀌면 시작 등록과 Explorer 연동을 다시 저장합니다. 광고나 사용 분석이 없고 앱 목록, 배치, 사진 선택은 업로드하지 않습니다. 구성한 데이터 제공자는 해당 요청을 받습니다. [보안(영어)](SECURITY.md)

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

[버그 및 제안](https://github.com/LastResolution/ReStart/issues/new/choose) · [기여(영어)](CONTRIBUTING.md) · [아키텍처(영어)](docs/architecture.md) · [테스트(영어)](docs/testing.md) · [릴리스](https://github.com/LastResolution/ReStart/releases)

ReStart는 후기 Windows 10 전체 화면 시작 메뉴에서 영감을 받은 독립 MIT 프로젝트이며 Microsoft와 제휴하지 않습니다. 이미지는 영어 데모, 합성 메트릭, 생성한 사진 색상을 사용하는 실제 앱입니다. 이름과 아이콘은 각 권리자에게 속합니다. [MIT 라이선스](LICENSE) · [지원(영어)](SUPPORT.md)

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
