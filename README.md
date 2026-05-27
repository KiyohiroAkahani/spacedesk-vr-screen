# VrDesktopBridge

**English** — Turn a phone (in a Cardboard‑style holder) into a VR "big
screen" of your Windows desktop. The PC screen is captured and shown
side‑by‑side with VR lens‑distortion correction on the spacedesk
**extended** display, while you keep using the real mouse and keyboard.
No account, no cloud.

**中文** — 把手机（放进 Cardboard 类头显支架）变成显示 Windows 桌面的 VR
“大屏幕”。本程序捕获 PC 画面，做左右并排 + VR 镜片畸变校正，通过 spacedesk
的**扩展**显示器投到手机；同时你照常使用真实的鼠标和键盘。无需账号、无需云。

**日本語** — スマホ（Cardboard型ホルダーに装着）を、Windowsデスクトップの
VR「大画面」にします。PC画面を取り込み、左右2分割＋VRレンズ歪み補正して
spacedeskの**拡張**ディスプレイへ表示。実際のマウス・キーボードはそのまま
使えます。アカウント不要・クラウド不要。

> Windows 10/11 only. No head tracking (a fixed VR screen). /
> 仅限 Windows 10/11，无头部追踪（固定 VR 画面）。/
> Windows 10/11 専用・ヘッドトラッキングなし（固定大画面）。

📘 **Step‑by‑step setup → [`docs/SETUP_GUIDE.md`](docs/SETUP_GUIDE.md)**
（分步设置指南 / 手順ガイド・英中日）

---

## 1. Prerequisites / 前置准备 / 必要な準備

**English**
1. **PC: install the spacedesk Driver / Server (Console)** from the
   official site: https://www.spacedesk.net/download/#server-driver
   ⚠ This is **NOT** the "spacedesk" app from the Microsoft Store
   (the Store app is not used on the PC side).
2. **Phone: install the spacedesk app** — "spacedesk - USB display for PC"
   (iPhone / Android).
3. **Connect the PC and the phone to the same Wi‑Fi.**
4. **Set the display to "extend".** After the phone is connected,
   right‑click the desktop → **"Display settings"** → in **"Multiple
   displays"** choose **"Extend these displays"**. ⚠ Do **NOT** choose
   **"Duplicate these displays"** — with Duplicate you cannot operate the
   screen. (Quick way: **Windows key + P** → **"Extend"**.)
5. Connect the phone in spacedesk so the extended display is active.
   Then start this app: the phone becomes a VR (side‑by‑side) screen and
   you can use the PC normally while watching it.
6. **A phone VR goggle is required.** Recommended (as of May 2026):
   the SHREVNI phone VR goggle on Amazon Japan —
   https://www.amazon.co.jp/dp/B0FP5M4L6B/

**中文**
1. **PC 端：安装 spacedesk Driver / Server（Console）**，从官网下载：
   https://www.spacedesk.net/download/#server-driver
   ⚠ **不是** Microsoft Store 里的 “spacedesk” 应用（商店版 PC 端不用）。
2. **手机端：安装 spacedesk 应用**——“spacedesk - USB display for PC”
   （iPhone / Android）。
3. **PC 和手机连接到同一个 Wi‑Fi。**
4. **把显示设为“扩展”。** 手机连接后，右键点击桌面 → **“显示设置”** →
   在 **“多显示器”** 中选择 **“扩展这些显示器”**。⚠ **不要**选
   **“复制这些显示器”**——复制模式下无法操作画面。
   （快捷方式：**Windows 键 + P** → **“扩展”**。）
5. 在 spacedesk 中连接手机使扩展显示器生效，然后启动本程序：手机变成
   VR（左右并排）画面，可一边观看一边正常操作 PC。
6. **需要一个手机 VR 头显。** 推荐（截至 2026 年 5 月）：Amazon 日本
   在售的 SHREVNI 手机 VR 头显 —
   https://www.amazon.co.jp/dp/B0FP5M4L6B/

**日本語**
1. **PC側：spacedesk Driver / Server（Console）をインストール**（公式）：
   https://www.spacedesk.net/download/#server-driver
   ⚠ Microsoft Store の「spacedesk」アプリ**ではありません**（Store版は
   PC側では使いません）。
2. **スマホ側：spacedesk アプリ**「spacedesk - USB display for PC」
   （iPhone / Android）をインストール。
3. **PCとスマホを同じ Wi‑Fi に接続。**
4. **ディスプレイを「拡張」にする。** スマホ接続後、デスクトップを
   右クリック → **「ディスプレイ設定」** → **「マルチ ディスプレイ」**
   で **「表示画面を拡張する」** を選びます。⚠ **「表示画面を複製する」
   は選ばないでください** — 複製だと画面操作ができません。
   （簡単な方法：**Windowsキー + P** → **「拡張」**。）
5. spacedeskでスマホを接続し拡張ディスプレイが出た状態で本アプリを起動 →
   スマホがVR（左右2画面）になり、見ながらPCを普通に操作できます。
6. **スマホ用VRゴーグルが必要です。** おすすめ（2026年5月時点）：
   Amazon の SHREVNI スマホ用VRゴーグル —
   https://www.amazon.co.jp/dp/B0FP5M4L6B/

---

## 2. Download & run / 下载与运行 / 入手と起動

**English** — Easiest: download **`VrDesktopBridge.exe`** from this
repository's **Releases** page and double‑click it (no .NET needed).
Or from source: `git clone` this repo and double‑click **`start.bat`**.
Per‑user settings are created automatically on first run at
`%LOCALAPPDATA%\VrDesktopBridge\config.json` (nothing machine‑specific is
shipped). Building from source needs the
[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

**中文** — 最简单：从本仓库的 **Releases** 页面下载 **`VrDesktopBridge.exe`**
并双击运行（无需 .NET）。或从源码：`git clone` 本仓库后双击 **`start.bat`**。
首次运行会在 `%LOCALAPPDATA%\VrDesktopBridge\config.json` 自动生成个人设置
（不包含任何机器相关信息）。从源码编译需要
[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

**日本語** — 最も簡単：本リポジトリの **Releases** から
**`VrDesktopBridge.exe`** をダウンロードしてダブルクリック（.NET不要）。
またはソースから：`git clone` 後 **`start.bat`** をダブルクリック。
個人設定は初回起動時に `%LOCALAPPDATA%\VrDesktopBridge\config.json` へ
自動生成されます（マシン固有情報は同梱しません）。ソースからのビルドには
[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) が必要です。

> 🎬 **Tip:** for a great first impression, watch a **4K video**
> full‑screen on the VR big screen — e.g.
> https://www.youtube.com/watch?v=Pt_2nw6vv1k&t=2589s /
> 推荐全屏观看 **4K 视频**感受效果 /
> **4K動画**を全画面で観るのがおすすめ

---

## 3. Controls / 操作 / 操作

**English** — Global hotkeys (work anywhere while wearing the headset).
A short on‑screen guide appears at the top of each eye at startup.

**中文** — 全局快捷键（戴着头显时在任何位置都有效）。启动时每只眼睛上方
会短暂显示操作提示。

**日本語** — グローバルホットキー（VR装着中・どのウィンドウでも有効）。
起動直後、各眼の上部に操作ガイドが数秒表示されます。

| Key / 按键 / キー | Action — 功能 — 内容 |
|---|---|
| **Esc ×2 (double‑tap)** | Quit — 退出 — 終了 |
| `Ctrl+Alt+Shift+Q` | Quit — 退出 — 終了 |
| `Ctrl+Alt+Shift+↑ / ↓` | Zoom in / out — 放大/缩小 — 拡大/縮小 (50–150%, start 80%) |
| `Ctrl+Alt+Shift+→ / ←` | IPD narrower / wider — 调整双眼像距 — 左右像中心の狭め/広げ (両眼のピントを合わせる) |
| `Ctrl+Alt+Shift+S` | Scale preset cycle — 缩放预设循环 — 縮尺巡回 (100→90→80→70) |
| `Ctrl+Alt+Shift+V` | VR lens distortion on/off — VR畸变校正开关 — 歪み補正 ON/OFF |
| `Ctrl+Alt+Shift+C` | Confine mouse to mirror — 鼠标限制在镜像内 — マウス拘束 ON/OFF |
| `Ctrl+Alt+Shift+W` | Pull stray windows back — 找回跑掉的窗口 — 迷子ウィンドウ回収 |
| `Ctrl+Alt+Shift+F` | Cycle target monitor — 切换目标显示器 — 表示先モニタ巡回 |
| `Ctrl+Alt+Shift+D` | Re‑detect displays — 重新检测显示器 — ディスプレイ再検出 |
| `Ctrl+Alt+Shift+A` | Exclude own window from capture — 自身窗口排除采集 — 自窓除外 |

**English** — From a terminal you can also quit with `Ctrl+C`, or run
`py -3 .\tools\stop_app.py` (a guaranteed stop). /
**中文** — 也可在终端按 `Ctrl+C`，或运行 `py -3 .\tools\stop_app.py`
（强制停止）。/
**日本語** — 端末からは `Ctrl+C`、または `py -3 .\tools\stop_app.py`
でも確実に終了できます。

---

## 4. Settings / 设置 / 設定

**English** — `config.json` is created on first run at
`%LOCALAPPDATA%\VrDesktopBridge\`. Key items below. /
**中文** — 首次运行在 `%LOCALAPPDATA%\VrDesktopBridge\` 生成 `config.json`，
主要项见下。/
**日本語** — 初回起動時に `%LOCALAPPDATA%\VrDesktopBridge\` へ
`config.json` を生成。主な項目は下記。

| Key | Default | Meaning — 含义 — 説明 |
|---|---|---|
| `TargetMonitorMatch` | `"spacedesk"` | Which monitor to display on / 显示到哪个显示器 / 表示先モニタ |
| `StartupScalePercent` | `80` | Startup zoom % / 启动缩放% / 起動時縮尺% |
| `LensDistortion` | `true` | VR lens correction / VR畸变校正 / レンズ歪み補正 |
| `DistortK1` / `DistortK2` | `0.22` / `0.10` | Distortion coefficients (tune per viewer) / 畸变系数（按头显调整）/ 歪み係数（ゴーグルに合わせ調整） |
| `ConfineCursor` | `true` | Keep mouse on the mirrored monitor / 鼠标限制在镜像显示器 / マウス拘束 |
| `KeepWindowsOnCapture` | `true` | Pull stray windows back into view / 把跑掉的窗口拉回 / 迷子ウィンドウ回収 |
| `Fps` | `60` | Frame cap / 帧率上限 / 描画上限 |

---

## 5. Build & publish (developers) / 编译与发布 / ビルドと配布

**English** — Use the Python tools in `tools/` (run with `py -3`).
`publish_app.py` produces a **self‑contained single .exe** in `dist\`
that needs no .NET on the target — attach it to a GitHub Release. /
**中文** — 使用 `tools/` 下的 Python 脚本（用 `py -3` 运行）。
`publish_app.py` 会在 `dist\` 生成**自包含单文件 .exe**，目标机无需 .NET，
适合作为 GitHub Release 附件分发。/
**日本語** — `tools/` の Python スクリプト（`py -3`）を使用。
`publish_app.py` は `dist\` に **.NET不要の自己完結単一exe** を生成
（GitHub Release に添付して配布）。

```
py -3 .\tools\clean_build.py     # clean build / 干净编译 / クリーンビルド
py -3 .\tools\run_app.py         # run (dev) / 运行(开发) / 起動(開発)
py -3 .\tools\publish_app.py     # make dist\VrDesktopBridge.exe
py -3 .\tools\stop_app.py        # force stop / 强制停止 / 確実に停止
```

---

## 6. How it works / 原理 / 仕組み

**English** — Capture: DXGI Desktop Duplication of the primary monitor.
Render: Direct3D 11 — side‑by‑side + aspect‑preserving + barrel
lens‑distortion shader + composited mouse cursor. Transport: shown
full‑screen on the spacedesk extended display → the phone shows VR. The
real mouse/keyboard drive the real desktop; stray windows are pulled
back into view automatically.

**中文** — 采集：对主显示器使用 DXGI Desktop Duplication。渲染：
Direct3D 11——左右并排 + 保持纵横比 + 桶形镜片畸变着色器 + 合成鼠标指针。
传输：在 spacedesk 扩展显示器上全屏显示 → 手机即为 VR 画面。真实鼠标/键盘
操作真实桌面；跑掉的窗口会被自动拉回可见区域。

**日本語** — キャプチャ：プライマリ画面を DXGI Desktop Duplication。
描画：Direct3D 11 — 左右2分割＋アスペクト保持＋樽型レンズ歪み補正
シェーダー＋マウスカーソル合成。転送：spacedesk拡張ディスプレイに
フルスクリーン表示 → スマホがVR画面。物理マウス/キーボードは実デスクトップ
に効き、迷子ウィンドウは自動で見える位置へ戻します。

> Tech: C# / .NET 8 / WPF / Vortice (Direct3D11/DXGI) / Win32. The repo
> uses a "WAT" layout (Workflows / Agents / Tools): SOPs in `workflows/`,
> deterministic scripts in `tools/` (see `CLAUDE.md`).

---

## 7. Troubleshooting / 故障排除 / トラブルシュート

**English**
- Nothing on the phone / can't operate → ensure spacedesk is in
  **Extend** (not Duplicate); press `Ctrl+Alt+Shift+D` to re‑detect.
- A folder/window you opened is invisible/unclickable → it is pulled
  back automatically by default (`Ctrl+Alt+Shift+W` toggles this).
- Looks distorted in VR → toggle `Ctrl+Alt+Shift+V` to compare; tune
  `DistortK1/K2` in `config.json` for your viewer.
- Mouse cursor stays hidden, etc. → run `tools\stop_app.py` to stop
  cleanly and restore the cursor.
- `.NET 8 not found` → use the self‑contained `VrDesktopBridge.exe`,
  or install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

**中文**
- 手机无画面 / 无法操作 → 确认 spacedesk 为**扩展**（非复制）；按
  `Ctrl+Alt+Shift+D` 重新检测。
- 打开的文件夹/窗口看不见、点不到 → 默认会自动拉回（`Ctrl+Alt+Shift+W`
  开关）。
- VR 中画面变形 → 用 `Ctrl+Alt+Shift+V` 对比开关；按头显调整 `config.json`
  的 `DistortK1/K2`。
- 鼠标指针一直不显示等 → 运行 `tools\stop_app.py` 可干净退出并恢复指针。
- 提示 `.NET 8 not found` → 使用自包含的 `VrDesktopBridge.exe`，或安装
  [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

**日本語**
- スマホに何も出ない/操作できない → spacedesk が**拡張**か確認（複製は
  不可）。`Ctrl+Alt+Shift+D` で再検出。
- 開いたフォルダ/ウィンドウが見えない・触れない → 既定で自動的に戻します
  （`Ctrl+Alt+Shift+W` でON/OFF）。
- VRで歪む → `Ctrl+Alt+Shift+V` で比較し、`config.json` の `DistortK1/K2`
  をゴーグルに合わせ調整。
- マウスカーソルが消えたまま等 → `tools\stop_app.py` で確実に終了し復帰。
- `.NET 8 not found` → 自己完結 `VrDesktopBridge.exe` を使うか、
  [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) を導入。

---

## 8. Limitations / 限制 / 制限

**English** — Windows 10/11 only. No head tracking (fixed VR screen).
Requires spacedesk in Extend mode (Duplicate cannot be operated). For
phone‑holder viewers (Cardboard‑style), not dedicated VR HMDs. /
**中文** — 仅 Windows 10/11。无头部追踪（固定 VR 画面）。需 spacedesk
扩展模式（复制模式无法操作）。面向手机支架式头显（Cardboard 类），不针对
专用 VR HMD。/
**日本語** — Windows 10/11 専用。ヘッドトラッキングなし（固定大画面）。
spacedesk は拡張モード前提（複製は操作不可）。スマホ装着型ビューア
（Cardboard 等）向けで専用VR HMD用ではありません。

---

## 9. License / 许可 / ライセンス

**English** — This project is released under the **MIT License** (see
[`LICENSE`](LICENSE)). spacedesk is third‑party software by datronicsoft,
unrelated to and not distributed with this project; follow its own license. /
**中文** — 本项目以 **MIT 许可证**发布（见 [`LICENSE`](LICENSE)）。
spacedesk 为 datronicsoft 的第三方软件，与本项目无关且不随附，请遵循其各自
许可。/
**日本語** — 本プロジェクトは **MIT ライセンス**で公開します
（[`LICENSE`](LICENSE) 参照）。spacedesk は datronicsoft 製の第三者ソフトで
本プロジェクトとは無関係・非同梱です。各自のライセンスに従ってください。

---

## 10. Special Thanks / 特别鸣谢 / 謝辞

**English** — This application was completed **100% through "vibe
coding"** — by describing intent in natural language and iterating with
an AI pair‑programmer. That this was possible at all is thanks to
**Claude Opus (Anthropic)** and, just as importantly, to the countless
**pioneers and giants of programming knowledge** whose accumulated work
every line here stands upon. With deep respect and gratitude — *"If I
have seen further, it is by standing on the shoulders of giants."*
And finally, heartfelt thanks to **my wife**, who became the very first
tester of this phone‑VR experience and gave invaluable feedback along
with a genuinely moved, delighted reaction that made it all worth it.

**中文** — 本应用是**100% 通过“氛围编程”(vibe coding)** 完成的——用自然
语言描述意图，并与 AI 结对编程不断迭代。这一切之所以可能，要感谢
**Claude Opus（Anthropic）**，同样要感谢历代**编程知识的先驱与巨人**——
此处每一行代码都站在他们累积的成果之上。致以深深的敬意与感谢——
*“如果说我看得更远，那是因为我站在巨人的肩膀上。”*
最后，衷心感谢**我的妻子**——她成为这款手机 VR 体验的第一位测试者，
给予了宝贵的意见，以及发自内心的感动与惊喜反应，让这一切都值得。

**日本語** — 本アプリは、自然言語で意図を伝えAIとペアプログラミングを
重ねる **「100% バイブコーディング」だけで完成**しました。これが可能で
あったのは **Claude Opus（Anthropic）**、そして同じく重要なこととして、
ここにある一行一行が積み上げの上に立つ、**プログラミングの知を築いてきた
先人たち・知の巨人たち**のおかげです。深い敬意と感謝を込めて——
*「巨人の肩の上に立つことで、より遠くを見渡せる」*
そして最後に、このスマホVRの**最初のユーザーテスター**となり、貴重な
意見と、心からの感動・歓喜のリアクションをくれた**妻**へ、心からの
感謝を込めて。
