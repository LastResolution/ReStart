<p align="center"><img src="assets/ReStart.png" width="96" alt="ReStart" /></p>
<h1 align="center">ReStart</h1>
<p align="center"><strong>让全屏开始菜单回归。</strong><br>你的应用、你的布局、你自己的动态磁贴。</p>

<!-- languages:start -->
<p align="center">
  <a href="README.md">English</a> · <a href="README.de.md">Deutsch</a> · <a href="README.es.md">Español</a> · <a href="README.fr.md">Français</a> · <a href="README.ja.md">日本語</a> · <a href="README.ko.md">한국어</a> · <a href="README.zh-CN.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a>
</p>
<!-- languages:end -->

<p align="center"><a href="https://lastresolution.github.io/ReStart/">网站（英语）</a> · <a href="https://lastresolution.github.io/ReStart/developers/">开发者指南（英语）</a></p>

> **AI 翻译：** 本指南使用 AI 从英文翻译，可能存在错误。[英文 README](README.md) 为主要参考，包含更多示例。欢迎修正翻译。截图使用英文演示环境和示例数据。

<p align="center"><strong>观看 60 秒演示</strong><br><sub>1080p · 英文 · 无音频</sub></p>

https://github.com/user-attachments/assets/a893cd20-8c82-4c47-b5fb-bf22743f67e1

怀念 Windows 10 的全屏开始菜单？ReStart 在 Windows 11 和 Windows 10 上为应用提供熟悉的空间。固定常用内容，自由安排布局，再加入可以用 JavaScript 编写的动态磁贴。

**便携 · JavaScript 动态磁贴 · x64 和 ARM64 · 八种语言 · 无需单独安装 .NET**

**触控与任务栏。** 支持触控拖动磁贴和分组、长按菜单、文件夹及滚动。启动设置可在 ReStart 打开时显示其所在屏幕上自动隐藏的任务栏。自动隐藏由 Windows 控制，ReStart 不提供独立的隐藏设置。每个已放置磁贴的数据连接均需“保存并授权”，访问请求改变后需要重新授权。[行为与验证范围（英文）](docs/fidelity.md)。

## 可靠性与私人备份

可暂停可见区域外的动态磁贴，并通过有限次数的重试恢复浏览器故障。两项功能均可单独设置。**设置 → 布局与数据**可一起备份和恢复布局、外观、已安装磁贴、草稿和背景。不包括连接设置、凭据、照片文件夹路径和缓存，但仍包含应用名称、启动路径及自定义源代码和资源。请将备份保持私密。[英文指南](docs/backups.md)。

## 更多布局控制与创作空间

可锁定全部布局或单个组。Ctrl+Shift+L 切换全局锁定；Ctrl+E 选择磁贴，Ctrl+方向键移动磁贴，Ctrl+Shift+方向键移动组，Alt+1–4 更改大小，Ctrl+G 将所选磁贴分组，Shift+F10 打开菜单。仍支持撤销/重做。还可保存独立于桌面的背景图片。

在**设置 → 外观**中，可将背景浓度设为 **10–100%**，背景图片模糊设为 **0–50**。模糊默认值为 **12**，设为 **0** 可显示清晰图片。高对比度设置优先。点击**结束选择**或**已固定的磁贴**可直接返回磁贴主页。

外观、磁贴缩放、组宽度和语言可在保存前预览。未保存时关闭会询问保存、放弃或继续编辑，放弃会恢复已保存的显示。主动下载的更新会保留准备就绪提醒，并在下次启动时恢复已验证的准备状态；普通启动不会自动安装。

内置 19 个 HTML/CSS/JavaScript 磁贴。Tile Studio 可编辑源码和清单，使用离线示例数据预览四种大小，再保存草稿或固定磁贴。新增天气风景、系统仪表板、RSS 新闻看板和 Canvas 花园；SVG 时钟指针按电脑时间运行。磁贴名称默认显示，可在设置中隐藏。

拖动时，磁贴、分组或多选项目的半透明副本会跟随指针。蓝色标记指示目标位置，被挤开的磁贴也会在放下前预览新位置。

![Tile Studio: HTML, CSS and JavaScript with four live previews](docs/images/tile-studio.png)

![Weather scene, System dashboard, News board and Canvas garden](docs/images/web-examples.png)

**编辑器操作：** 支持 JavaScript、HTML、CSS 和 JSON 语法高亮，<kbd>Tab</kbd> / <kbd>Shift</kbd> + <kbd>Tab</kbd> 缩进，以及 **Format document / Ctrl+Shift+F** 离线格式化；一次撤销即可恢复。Feed headlines 和 News board 在磁贴内部滚动。关闭的文件夹显示大小一致的动态预览。初始和重置布局使用随附的 Month dots 和 Orbit clock。可选的启动更新检查会通过可关闭的通知提示新版本。

## 快速开始

1. Intel/AMD 电脑下载 **[x64 ZIP](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-x64.zip)**；Windows on ARM 下载 **[ARM64 ZIP](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-arm64.zip)**。
2. 解压**整个 ZIP**。保留 `ReStart.exe` 旁的 `TileHost`、`Examples` 和 `third-party`。
3. 运行 **ReStart.exe**，按 **Windows 键**并输入应用名称。也可以使用 <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>Space</kbd> 或托盘菜单打开。

齿轮设置中可选择主题、登录启动、Windows 键/开始按钮集成及更新。正常使用不需要管理员权限。提供 [SHA-256 校验值](https://github.com/LastResolution/ReStart/releases/latest)。

## Windows 应用集成

搜索还包含 Windows 提供的设置关键词及控制面板项目。可选 AQS 提供原生文件筛选及相关性排序；选择 **在 Windows 中搜索** 后，才会将查询交给 Windows 的其他搜索来源。

Windows 应用使用次数与 Windows 磁贴、徽章的只读兼容是两个独立选项，默认均关闭。兼容常用尺寸布局、本地图像及数据绑定，不会恢复已停用的推送服务；没有可用数据时保留应用图标。

[设置与兼容性说明](docs/start-behavior.md) · [支持范围](docs/fidelity.md)

## 功能

![ReStart 的打开和关闭动画](docs/images/start-motion.gif)

**默认使用分组，需要时自由放置。** 在空白处放下会创建未命名组；在磁贴上停留会创建文件夹。拖动时按住 <kbd>Shift</kbd> 可重新排列而不创建文件夹，按住 <kbd>Alt</kbd> 可在空白处独立放置。Settings → Start behavior → **Use free tile placement** 可将独立放置设为默认。现有布局和文件夹保持不变。相邻磁贴平滑移位，指针下的目标判定保持稳定。

拖动时，屏幕底部会短暂显示快捷键提示，可在开始菜单行为设置中关闭。按 <kbd>F1</kbd>，或在设置中打开 **键盘快捷键**，可查看完整列表。


| 类别 | 包含功能 |
| --- | --- |
| 打开和关闭 | 可选接管 Windows 键和任务栏开始按钮、Ctrl+Alt+Space、托盘控制、前台激活、切换应用/任务栏时关闭、按显示器工作区域显示、单实例 |
| 启动 | 可选登录启动、后台模式、跟随主题的准备画面和提前输入、图标缓存、后台应用目录刷新 |
| 磁贴和分组 | 四种尺寸、独立磁贴、按内容调整大小的可移动矩形分组、横向半格锚点、自动放置、双向滚动 |
| 文件夹 | 在分组内重叠创建、原位展开、添加/移除/排序、内联重命名和解散；只剩一个项目时仍保留文件夹 |
| 编辑布局 | 编辑模式、多选、批量移动、会话内撤销/重做；取消拖动保留原布局 |
| 拖动反馈 | 区分分组/文件夹目标、停留进度、可选提示及分组边界、边缘自动滚动；自动创建分组/文件夹可关闭 |
| 固定内容 | 应用、文件、目录、快捷方式、网站；资源管理器 **Pin to ReStart**、右键菜单、从应用列表拖动、明确指定目标 |
| 搜索 | 规范化应用名称、可执行文件名和系统声明的别名；63 个本地化 Windows 设置页面及英文别名，可选的索引文件名和需主动开启的文档内容搜索（最多 50 个本地结果），方向键/Enter 导航与过期查询取消 |
| Windows 会话 | 通过 Windows 登录界面切换用户；根据设备能力和策略显示睡眠、休眠，提供关机、重启及 Windows 更新等待重启时的更新电源操作 |
| 所有应用 | 使用 Windows 语言分类的自适应列（假名、韩文声母、拼音和笔画）、展开开始菜单目录、可滚动的字母选择器、Unicode 首字导航、实时刷新安装/卸载、最近添加和可选最常使用 |
| 应用命令 | 按场景显示菜单：开始界面的“更多”、搜索中的直接操作、文件/文件夹位置、已安装应用管理/评价及 Windows 允许的命令。可选的最近/常用文档提供独立操作；还可隐藏应用历史条目并个性化导航列表。[菜单和 Windows 10 支持范围](docs/context-menus.md) 还支持应用注册的任务，以及与 Windows 共享的跳转列表固定和取消固定。历史记录、任务和固定项目可分别控制显示。 共享仅适用于受支持 Windows 系统上可访问的实际文件；属性适用于存在的文件、文件夹或原始快捷方式。不可用的操作会隐藏。 应用自定义跳转列表类别保留其注册名称和原始操作。 |
| 启动选项 | 每个磁贴独立启用、参数、工作目录、受支持桌面应用/快捷方式的管理员运行；Windows 负责 UAC |
| 导航 | 可选悬停展开、单独选择文档/下载/音乐/图片/视频/网络/个人文件夹、账户和电源操作 |
| 外观 | 五种色系及各自的明暗变体、系统模式、自定义 RGB 背景/强调色、桌面壁纸、开始菜单着色、单个磁贴颜色、主题控件/滚动条、高对比度、可选动画 |
| 动态磁贴 | 19 个 JavaScript 示例、四种响应式尺寸、本地点击动作、实时更新开关、数据配置、创建示例、导入包、自动监视本地库 |
| 磁贴 API | 电脑时间、语言/尺寸、保留状态、CPU/内存/网络/电池/存储、所选照片、缓存 JSON/Web API/RSS/Atom/文件输入、本地应用集成 |
| 语言 | 英语、日语、德语、法语、西班牙语、韩语、简体中文、繁体中文，包括内置磁贴文案 |
| 数据和更新 | 本地设置、布局导入/导出/重置、备份和迁移、可清除的应用活动、版本/构建/许可证、无需 API 密钥的检查、带验证和恢复的便携更新、诊断报告预览 |
| 分发 | Windows 10/11 x64 和 ARM64 便携包、内含运行时和声明、可逆集成、不修改 Explorer |

[开关与默认值（英文）](docs/start-behavior.md) · [兼容性（英文）](docs/fidelity.md)

**实用的初始布局。** 首次启动和重置布局时，会根据应用标识及精确的本地化名称，按用途选择已安装的应用。应用不足时，用不重复的内置离线磁贴补充，使每组至少有六个磁贴。已有的已保存布局保持不变。[选择与重置方式（英文）](docs/start-behavior.md#initial-layout-and-reset)。

**实际测量的验证范围。** 原生检查验证两块屏幕在 100%/125%/150% 缩放及横竖屏混合配置下的窗口位置和尺寸。辅助功能提供程序已在八种语言、两种主题下检查；Narrator 的基本播报内容已通过语音回顾确认。在独立的测试界面中，还通过实机确认了触控点击、长按菜单、磁贴与分组拖动、文件夹创建和滚动。屏幕重新连接、其他触控场景和屏幕阅读器的完整交互仍需额外实机验证。[结果与设备覆盖范围（英文）](docs/fidelity.md#observed-platform-coverage)。

## 搜索与配置

**网页和云端。** 选择**在网上搜索…**，即可通过默认浏览器将当前搜索词传给 Bing。**搜索 Windows 和云端**会打开 Windows 搜索，请在该窗口输入搜索词；云端可用性由 Windows 和已连接的帐户决定。仅输入文字不会对外发送内容。这两个操作均可关闭。

**应用历史记录。** 启用的常用应用历史记录可排列同等匹配的结果；完全匹配的名称和可执行文件别名保持优先。最近添加的应用先显示三项，可用**显示更多／收起**展开。遵循 Windows 快捷方式的历史记录排除设置。不复制 Windows 开始菜单的内部排名或安装历史记录。[行为和限制（英文）](docs/start-behavior.md#app-and-document-history)。

![应用、Windows 设置及本地文件搜索结果](docs/images/unified-search.png)

在 **Settings → Start behavior** 中分别关闭设置/文件搜索、文档菜单、悬停效果、自动分组/文件夹、边缘滚动、拖动提示。最常使用应用的记录默认关闭。

| 开始菜单行为 | 每个磁贴的启动选项 |
| --- | --- |
| ![行为设置](docs/images/start-behavior.png) | ![参数、工作目录和管理员运行](docs/images/launch-options.png) |

关闭 **Use custom launch options** 时仍保留填写内容。未启用时，快捷方式保持 Windows 的常规启动设置。

## 动态磁贴与数据

![内置动态磁贴](docs/images/live-tiles.png)

- **时间与专注：** Orbit clock、Digital clock、Month dots、Focus cycle、Year in motion、Countdown。
- **电脑：** CPU pulse、Memory flow、Network flow、Battery、Storage。
- **照片：** Photo moments，选择文件夹，点击切换下一张。
- **连接数据：** Weather、Feed headlines、JSON monitor。

右键背景 → **Add live tile**。点击执行磁贴本地动作，例如暂停计时或切换照片。Weather/Feed/JSON 在 **Configure data sources…** 中填写网址或选择文件并保存。**Refresh data now** 可请求最新数据。

**天气初始使用柏林示例坐标，不是电脑的位置。** ReStart 不推测你的地区。请按[天气指南](docs/javascript-tiles.md#weather-location)修改坐标和显示名称。凭据按当前 Windows 用户加密并排除在布局导出之外。连接失败时可显示缓存；隐藏开始菜单后停止读取。

Web 磁贴包含 manifest.json、index.html、tile.css、tile.js 及可选 assets。在四种固定大小内可自由使用 HTML/CSS、SVG、Canvas 和透明背景，仍兼容原生 JS 包。需要 WebView2 Runtime；直接联网必须声明能力并逐磁贴授权，不提供通用文件、CLR 或 shell 桥接。 [HTML/CSS/JavaScript API](docs/javascript-tiles.md) · [Examples](examples/tiles)

## 操作

| 操作 | 结果 |
| --- | --- |
| Windows 键 / <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>Space</kbd> | 打开或关闭 |
| 输入 / <kbd>Ctrl</kbd> + <kbd>F</kbd> | 搜索；Enter 启动高亮结果，方向键更改选择 |
| <kbd>Esc</kbd> | 依次关闭搜索、文件夹或叠加层，再关闭开始菜单 |
| 拖动磁贴 / 分组标题 | 移动；空白处创建未命名组 |
| <kbd>Alt</kbd> + 拖到空白处 | 仅本次独立放置 |
| <kbd>Shift</kbd> + 拖动 | 移动相邻磁贴以重新排列，不触发重叠分组或文件夹操作 |
| 在磁贴上重叠约 0.6 秒 | 预览就绪后创建文件夹；已有文件夹接收添加项 |
| 侧栏/背景菜单的 **选择磁贴** / <kbd>Ctrl</kbd> + <kbd>E</kbd> | 多选而不打开应用；平时也可以直接拖动磁贴 |
| 编辑时 <kbd>Ctrl</kbd> + <kbd>A</kbd> / <kbd>Delete</kbd> | 选择根磁贴 / 取消固定所选项 |
| <kbd>Ctrl</kbd> + <kbd>Z</kbd> / <kbd>Ctrl</kbd> + <kbd>Y</kbd> 或 <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Z</kbd> | 撤销 / 重做布局 |
| 铅笔 / 双击名称 | 内联重命名分组或文件夹 |
| <kbd>Shift</kbd> + 滚轮 | 横向滚动 |

Windows 组合键会转发给系统。布局历史在会话内保留最多 100 次修改、占用不超过 32 MiB；文本框保留自身的文本撤销。再次打开会回到固定磁贴主界面。

## 设置、隐私和更新

设置位于 `%LOCALAPPDATA%\ReStart\layout.json`。可选应用活动单独保存在 `activity.json`，可以清除。迁移保留备份，参见[说明（英文）](docs/migration.md)。About 显示版本、构建和许可证。

更新检查读取公开 GitHub Releases，无需 API 密钥。在设置 → 更新中选择“下载更新”，验证后选择“重启并更新”。可写文件夹中的完整、未修改便携包支持验证更新，并在安装或启动失败时恢复以前的文件。开发版本和旧包仍可手动替换。关于 → 诊断信息可预览并复制不含路径、应用列表、磁贴数据、凭据或原始日志的报告。不会自动上传。[更新和诊断（英文）](docs/updates.md)。位置改变时重新保存启动注册和资源管理器集成。没有广告或使用分析，不上传应用列表、布局或照片选择。配置的数据提供方会收到相应请求。[安全（英文）](SECURITY.md)

卸载时关闭登录启动和资源管理器集成，从托盘退出并删除安装目录。只有同时想删除设置和自定义磁贴时才删除本地数据目录。

## 自行构建

使用 Windows 和 `global.json` 选择的稳定版 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。替换文件前先退出正在运行的应用。

```powershell
git clone https://github.com/LastResolution/ReStart.git
cd ReStart
dotnet restore ReStart.slnx --locked-mode
dotnet build ReStart.slnx -c Release --no-restore
dotnet test ReStart.slnx -c Release --no-build
.\src\ReStart.App\bin\Release\net10.0-windows\ReStart.exe
# 隔离预览：
dotnet run --project src/ReStart.App -c Release -- --preview
# 内含运行时的便携包：
./scripts/package.ps1 -Runtime win-x64
```

ARM64 使用 `win-arm64`。ZIP 和校验文件生成在 `artifacts/packages`。请始终分发完整 ZIP。

[问题与作品分享](https://github.com/LastResolution/ReStart/discussions) · [报告缺陷](https://github.com/LastResolution/ReStart/issues/new/choose) · [参与贡献（英文）](CONTRIBUTING.md) · [架构（英文）](docs/architecture.md) · [测试（英文）](docs/testing.md) · [发布](https://github.com/LastResolution/ReStart/releases)

ReStart 是受后期 Windows 10 全屏开始菜单启发的独立 MIT 项目，与 Microsoft 无关联。图像展示真实应用的英文演示、合成指标和生成的照片色块。名称和图标归各权利人所有。[MIT 许可证](LICENSE) · [第三方声明（英文）](docs/third-party.md) · [支持（英文）](SUPPORT.md)

---

<!-- support:start -->
<h2 align="center">支持独立开发</h2>
<p align="center">
  喜欢 ReStart？欢迎支持 LastResolution 持续改进 ReStart，并开发更多实用工具。<br>
  <sub>ReStart 免费且开源，支持完全自愿。</sub>
</p>
<p align="center">
  <a href="https://buymeacoffee.com/LastResolution"><img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" height="48" alt="Buy Me a Coffee · LastResolution" /></a>
</p>
<!-- support:end -->
