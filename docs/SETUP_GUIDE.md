# Setup Guide / 安装指南 / セットアップガイド

A careful, beginner‑friendly walkthrough. Do the steps **in order**.
The display setting in Step 3 is the part people get wrong most often,
so it is written out in full.

面向初学者的详细步骤，请**按顺序**操作。第 3 步的显示设置最容易出错，
因此写得很详细。

初めての方向けに丁寧に説明します。**順番どおり**に進めてください。
第3ステップのディスプレイ設定が一番つまずきやすいので、画面の文言の
ままに細かく書いています。

---

## 0. What you need / 需要准备 / 必要なもの

**English** — A Windows 10/11 PC; a smartphone (iPhone or Android); a
phone VR goggle that fits your phone (see Step 4); the PC and phone able
to join the **same Wi‑Fi**.

**中文** — 一台 Windows 10/11 电脑；一部智能手机（iPhone 或 Android）；
一个适配你手机的手机 VR 头显（见第 4 步）；电脑和手机能连到**同一个
Wi‑Fi**。

**日本語** — Windows 10/11 のPC、スマートフォン（iPhone か Android）、
自分のスマホに合うスマホ用VRゴーグル（手順4）、PCとスマホを**同じ
Wi‑Fi**につなげる環境。

---

## Step 1. Install the two spacedesk parts / 安装 spacedesk / spacedesk を入れる

**English**
- **On the PC:** open https://www.spacedesk.net/ in a browser → click
  **Download** → download **"spacedesk DRIVER for Windows (Server)"**
  (also called the *Console*) → run the installer and finish it.
  ⚠ Do **not** install the "spacedesk" app from the **Microsoft Store** —
  that one is for *viewing*, not for the PC server. Use the file from the
  website above.
- **On the phone:** open the App Store (iPhone) or Google Play (Android),
  search **"spacedesk"**, and install the app named
  **"spacedesk - USB display for PC"**.

**中文**
- **在电脑上：** 用浏览器打开 https://www.spacedesk.net/ →点击
  **Download** →下载 **“spacedesk DRIVER for Windows (Server)”**
  （也叫 *Console*）→运行安装程序并完成安装。
  ⚠ **不要**安装 **Microsoft Store** 里的 “spacedesk” 应用——那个是
  *观看端*，不是电脑服务器端。请使用上面网站的文件。
- **在手机上：** 打开 App Store（iPhone）或 Google Play（Android），
  搜索 **“spacedesk”**，安装名为
  **“spacedesk - USB display for PC”** 的应用。

**日本語**
- **PC側：** ブラウザで https://www.spacedesk.net/ を開く → **Download**
  をクリック → **「spacedesk DRIVER for Windows (Server)」**（＝*Console*）
  をダウンロード → インストーラーを実行して最後まで進めます。
  ⚠ **Microsoft Store の「spacedesk」アプリは入れないでください。**
  あれは*見る側*用で、PC側のサーバーではありません。必ず上記サイトの
  ファイルを使います。
- **スマホ側：** App Store（iPhone）または Google Play（Android）で
  **「spacedesk」** を検索し、**「spacedesk - USB display for PC」**
  という名前のアプリを入れます。

---

## Step 2. Same Wi‑Fi, then get this app / 同一 Wi‑Fi 与获取本应用 / 同じWi‑Fiと本アプリ入手

**English**
1. Make sure the **PC and the phone are on the same Wi‑Fi** (the same
   network name / SSID).
2. Get the app: open the GitHub page
   **github.com/KiyohiroAkahani/spacedesk-vr-screen** → on the right side
   click **"Releases"** → under the latest release click
   **`VrDesktopBridge.exe`** to download it.
3. The first time you run it, Windows may show **"Windows protected your
   PC"** (SmartScreen, because the app is not code‑signed). Click
   **"More info"** → **"Run anyway"**. This is expected for small
   open‑source tools.
   *(Advanced users can instead `git clone` the repo and double‑click
   `start.bat`.)*

**中文**
1. 确认**电脑和手机连接到同一个 Wi‑Fi**（相同的网络名称 / SSID）。
2. 获取应用：打开 GitHub 页面
   **github.com/KiyohiroAkahani/spacedesk-vr-screen** →在右侧点击
   **“Releases”** →在最新发布下点击 **`VrDesktopBridge.exe`** 下载。
3. 首次运行时，Windows 可能弹出 **“Windows 已保护你的电脑”**
   （SmartScreen，因为程序未做代码签名）。点击 **“更多信息”** →
   **“仍要运行”**。对小型开源工具来说这是正常现象。
   *（进阶用户也可 `git clone` 仓库后双击 `start.bat`。）*

**日本語**
1. **PCとスマホが同じWi‑Fi**（同じネットワーク名／SSID）に
   つながっていることを確認します。
2. アプリを入手：GitHubのページ
   **github.com/KiyohiroAkahani/spacedesk-vr-screen** を開き、
   画面右側の **「Releases」** をクリック → 最新リリースの
   **`VrDesktopBridge.exe`** をクリックしてダウンロード。
3. 初回起動時、Windows が **「Windows によって PC が保護されました」**
   （SmartScreen。署名のない個人アプリのため）と出ることがあります。
   **「詳細情報」** → **「実行」** を押してください。小さな
   オープンソースアプリでは正常な動作です。
   *（上級者は `git clone` して `start.bat` をダブルクリックでも可。）*

---

## Step 3. Connect & set the display (most important) / 连接并设置显示（最重要）/ 接続とディスプレイ設定（最重要）

**English**
1. **On the PC**, start **spacedesk** (the Console you installed in
   Step 1). Leave it running.
2. **On the phone**, open the **spacedesk** app. Your PC's name should
   appear in the list — **tap it to connect**. Your PC desktop now shows
   on the phone.
3. **Now set the display to "extend".** Right‑click an empty area of the
   Windows desktop → click **"Display settings"**. Scroll to the
   **"Multiple displays"** section and open the drop‑down. Choose
   **"Extend these displays"**.
   ⚠ Do **NOT** choose **"Duplicate these displays"** — with *Duplicate*
   you will not be able to operate the screen and the app will not work.
   *Tip:* a quick alternative is to press **Windows key + P** and choose
   **"Extend"**.
4. You should now have **two displays** (your PC screen + the phone as a
   second screen). Only then continue.

**中文**
1. **在电脑上**启动第 1 步安装的 **spacedesk**（Console），保持运行。
2. **在手机上**打开 **spacedesk** 应用。列表中应出现你的电脑名称——
   **点击它进行连接**。电脑桌面随即显示在手机上。
3. **现在把显示设为“扩展”。** 在 Windows 桌面空白处点右键 →点击
   **“显示设置”**。滚动到 **“多显示器”** 部分，打开下拉框，选择
   **“扩展这些显示器”**。
   ⚠ **不要**选 **“复制这些显示器”**——*复制*模式下无法操作画面，
   程序无法工作。
   *小技巧：* 也可按 **Windows 键 + P**，选择 **“扩展”**。
4. 此时应有**两个显示器**（电脑屏幕 + 作为第二屏的手机）。确认后再继续。

**日本語**
1. **PCで**、手順1で入れた **spacedesk**（Console）を起動し、
   そのままにしておきます。
2. **スマホで** **spacedesk** アプリを開きます。一覧にPCの名前が
   出てくるので、**それをタップして接続**します。スマホにPCの
   デスクトップが映ります。
3. **ここでディスプレイを「拡張」にします。** デスクトップの何もない
   ところを**右クリック** → **「ディスプレイ設定」** をクリック。
   **「マルチ ディスプレイ」**（複数ディスプレイ）の項目までスクロール
   し、ドロップダウンを開いて **「表示画面を拡張する」** を選びます。
   ⚠ **「表示画面を複製する」は選ばないでください。** *複製*にすると
   画面が操作できず、本アプリは動作しません。
   *ヒント：* **Windowsキー + P** を押して **「拡張」** を選んでも
   同じことができます（簡単）。
4. これで**ディスプレイが2つ**（PC画面＋2台目としてスマホ）になって
   いるはずです。確認できたら次へ進みます。

---

## Step 4. Put the phone in the goggle & start the app / 装入头显并启动 / ゴーグル装着とアプリ起動

**English**
1. You need a **phone VR goggle** that fits your phone. Recommended
   (as of May 2026): the **SHREVNI** phone VR goggle on Amazon Japan
   (external buttons, aspheric lenses, 120° wide view, glasses‑friendly,
   fits 5–7 inch iPhone/Android, lightweight):
   https://www.amazon.co.jp/dp/B0FP5M4L6B/
2. Double‑click **`VrDesktopBridge.exe`** (the file from Step 2).
3. The phone view becomes a **side‑by‑side VR image**. A short guide
   appears at the top of each eye. Put the phone into the goggle and
   wear it.
4. Use your **normal mouse and keyboard** — what you see in VR is your
   real desktop.

**中文**
1. 你需要一个适配你手机的**手机 VR 头显**。推荐（截至 2026 年 5 月）：
   Amazon 日本在售的 **SHREVNI** 手机 VR 头显（外部按钮、非球面镜片、
   120° 广角、适合戴眼镜、支持 5～7 英寸 iPhone/Android、轻量）：
   https://www.amazon.co.jp/dp/B0FP5M4L6B/
2. 双击第 2 步下载的 **`VrDesktopBridge.exe`**。
3. 手机画面变为**左右并排的 VR 图像**，每只眼睛上方会短暂显示提示。
   把手机放入头显并戴上。
4. 照常使用**鼠标和键盘**——你在 VR 中看到的就是真实的桌面。

**日本語**
1. 自分のスマホに合う**スマホ用VRゴーグル**が必要です。おすすめ
   （2026年5月時点）：Amazon の **SHREVNI** スマホ用VRゴーグル
   （外部コントロールボタン、非球面光学レンズ、120°超広角、眼鏡対応、
   5〜7インチ iPhone/Android 対応、軽量）：
   https://www.amazon.co.jp/dp/B0FP5M4L6B/
2. 手順2で入手した **`VrDesktopBridge.exe`** をダブルクリック。
3. スマホの表示が**左右2分割のVR映像**になり、各眼の上に操作ガイドが
   数秒出ます。スマホをゴーグルに入れて装着します。
4. **いつものマウス・キーボード**で操作してください。VRで見えている
   のは本物のデスクトップです。

---

## Step 5. Everyday use / 日常使用 / ふだんの使い方

**English**
- **Quit:** tap **Esc twice quickly** (or `Ctrl+Alt+Shift+Q`).
- **Zoom in / out:** `Ctrl+Alt+Shift+↑ / ↓`.
- If a window you open seems missing, it is automatically pulled back
  into view; you can toggle this with `Ctrl+Alt+Shift+W`.
- Full key list, settings (`%LOCALAPPDATA%\VrDesktopBridge\config.json`)
  and troubleshooting are in the [README](../README.md).

**中文**
- **退出：** 快速按 **两次 Esc**（或 `Ctrl+Alt+Shift+Q`）。
- **缩放：** `Ctrl+Alt+Shift+↑ / ↓`。
- 若打开的窗口好像不见了，它会被自动拉回可见区域；可用
  `Ctrl+Alt+Shift+W` 切换此功能。
- 完整按键、设置（`%LOCALAPPDATA%\VrDesktopBridge\config.json`）与
  排错见 [README](../README.md)。

**日本語**
- **終了：** **Esc を素早く2回**（または `Ctrl+Alt+Shift+Q`）。
- **拡大／縮小：** `Ctrl+Alt+Shift+↑ / ↓`。
- 開いたウィンドウが見当たらないときは自動で見える位置へ戻ります。
  `Ctrl+Alt+Shift+W` でON/OFF切替できます。
- 全キー・設定（`%LOCALAPPDATA%\VrDesktopBridge\config.json`）・
  トラブルは [README](../README.md) を参照。

---

## If something goes wrong / 出现问题时 / うまくいかないとき

**English** — Most problems are the display mode. Re‑check Step 3:
right‑click desktop → *Display settings* → **"Extend these displays"**
(NOT "Duplicate"). If the phone shows nothing, reconnect spacedesk on the
phone, then press `Ctrl+Alt+Shift+D` in the app to re‑detect displays.

**中文** — 多数问题出在显示模式。请重新检查第 3 步：右键桌面 →
*显示设置* → **“扩展这些显示器”**（不是“复制”）。若手机无画面，先在
手机端重连 spacedesk，再在程序内按 `Ctrl+Alt+Shift+D` 重新检测显示器。

**日本語** — 多くの不具合はディスプレイ設定が原因です。手順3を再確認：
デスクトップ右クリック → *ディスプレイ設定* → **「表示画面を拡張する」**
（「複製」ではない）。スマホに何も出ないときは、スマホ側で spacedesk を
つなぎ直し、アプリ内で `Ctrl+Alt+Shift+D` を押して再検出してください。
