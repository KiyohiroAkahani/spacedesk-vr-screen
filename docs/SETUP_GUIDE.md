# Setup Guide / 安装指南 / セットアップガイド

This is the full step‑by‑step setup (also usable as an outline for an
install video). Order matters — especially the **"Extend" display mode**.
本指南为完整的分步设置（也可作为安装视频的脚本大纲）。顺序很重要——
尤其是**“扩展”显示模式**。
これはインストールの全手順（紹介動画の構成案としても使えます）。
順番が重要です。特に **「拡張」ディスプレイモード**。

---

## 0. What you need / 需要准备 / 必要なもの

**English**
- A Windows 10/11 PC and a smartphone (iPhone / Android).
- A **phone VR goggle** that fits your phone (see section 3).
- PC and phone on the **same Wi‑Fi**.

**中文**
- 一台 Windows 10/11 电脑和一部智能手机（iPhone / Android）。
- 一个适配你手机的**手机 VR 头显**（见第 3 节）。
- 电脑和手机连接到**同一个 Wi‑Fi**。

**日本語**
- Windows 10/11 のPCとスマートフォン（iPhone / Android）。
- 自分のスマホに合う**スマホ用VRゴーグル**（第3節参照）。
- PCとスマホを**同じWi‑Fi**に接続。

---

## 1. Install / 安装 / 導入

**English**
1. **PC — spacedesk Driver/Server (Console)** from the official site
   https://www.spacedesk.net/download/#server-driver
   ⚠ This is **NOT** the "spacedesk" app in the Microsoft Store
   (the Store app is not used on the PC side).
2. **Phone — "spacedesk - USB display for PC"** from the App Store /
   Google Play.
3. **Connect the PC and the phone to the same Wi‑Fi.**
4. **Get this app**: download `VrDesktopBridge.exe` from this
   repository's **Releases** page.
   (Advanced: `git clone` the repo and double‑click `start.bat`.)

**中文**
1. **PC——spacedesk Driver/Server（Console）**，从官网下载：
   https://www.spacedesk.net/download/#server-driver
   ⚠ **不是** Microsoft Store 里的 “spacedesk” 应用（商店版 PC 端不用）。
2. **手机——“spacedesk - USB display for PC”**，从 App Store /
   Google Play 安装。
3. **将 PC 和手机连接到同一个 Wi‑Fi。**
4. **获取本应用**：从本仓库 **Releases** 页面下载
   `VrDesktopBridge.exe`。（进阶：`git clone` 后双击 `start.bat`。）

**日本語**
1. **PC — spacedesk Driver/Server（Console）** を公式から導入：
   https://www.spacedesk.net/download/#server-driver
   ⚠ Microsoft Store の「spacedesk」アプリ**ではありません**
   （Store版はPC側では使いません）。
2. **スマホ — 「spacedesk - USB display for PC」** を App Store /
   Google Play から導入。
3. **PCとスマホを同じWi‑Fiに接続。**
4. **本アプリを入手**：本リポジトリの **Releases** から
   `VrDesktopBridge.exe` をダウンロード。
   （上級者：`git clone` して `start.bat` をダブルクリック。）

---

## 2. Launch order / 启动顺序 / 起動の順番

**English**
1. **Start spacedesk on the PC** (Console) and confirm it is running.
2. **Open spacedesk on the phone** and tap your PC to connect — your
   desktop appears on the phone.
3. **Set the display to "Extend".** Right‑click the Windows desktop →
   *Display settings* → set multiple displays to **"Extend these
   displays"**. ⚠ **NOT "Duplicate these displays"** — Duplicate makes
   the app unusable (you cannot operate the screen).
4. **Launch `VrDesktopBridge.exe`.** The phone switches to a
   side‑by‑side VR view; a short on‑screen guide shows the controls.

**中文**
1. **在 PC 上启动 spacedesk**（Console），确认其正在运行。
2. **打开手机上的 spacedesk**，点选你的 PC 连接——桌面随即显示在手机上。
3. **将显示设置为“扩展”。** 右键点击 Windows 桌面 →“显示设置”→
   将多显示器设为**“扩展这些显示器”**。⚠ **不要选“复制这些显示器”**
   ——复制模式下本应用无法操作（无法操作画面）。
4. **启动 `VrDesktopBridge.exe`。** 手机切换为左右并排的 VR 画面，
   屏幕上会短暂显示操作提示。

**日本語**
1. **PCで spacedesk（Console）を起動**し、待機状態を確認。
2. **スマホの spacedesk を開き**、PCをタップして接続 — スマホに
   デスクトップが映ります。
3. **ディスプレイを「拡張」に設定。** デスクトップを右クリック →
   *ディスプレイ設定* → 複数ディスプレイを **「表示画面を拡張する」** に。
   ⚠ **「表示画面を複製する」は不可** — 複製だと操作できずエラーに
   なります。
4. **`VrDesktopBridge.exe` を起動。** スマホが左右2分割のVR表示に
   なり、操作ガイドが数秒表示されます。

---

## 3. Phone VR goggle / 手机 VR 头显 / スマホ用VRゴーグル

**English** — You need a phone VR goggle that fits your phone. As of
May 2026 we recommend the **SHREVNI** phone VR goggle on Amazon Japan
(external control buttons, aspheric lenses, 120° wide view, glasses‑
friendly, fits 5–7 inch iPhone/Android, lightweight):
https://www.amazon.co.jp/dp/B0FP5M4L6B/

**中文** — 你需要一个适配你手机的手机 VR 头显。截至 2026 年 5 月，推荐
Amazon 日本在售的 **SHREVNI** 手机 VR 头显（外部控制按钮、非球面镜片、
120° 广角、适合戴眼镜、支持 5～7 英寸 iPhone/Android、轻量）：
https://www.amazon.co.jp/dp/B0FP5M4L6B/

**日本語** — 自分のスマホに合うスマホ用VRゴーグルが必要です。2026年5月
時点のおすすめは Amazon の **SHREVNI** スマホ用VRゴーグル（外部
コントロールボタン、非球面光学レンズ、120°超広角、眼鏡対応、
5〜7インチ iPhone/Android 対応、軽量）：
https://www.amazon.co.jp/dp/B0FP5M4L6B/

---

## 4. Quick controls / 快捷操作 / 主な操作

**English** — Quit: tap **Esc twice**. Zoom: **Ctrl+Alt+Shift+↑ / ↓**.
Full key list, settings and troubleshooting are in the
[README](../README.md).

**中文** — 退出：快速按 **Esc 两次**。缩放：**Ctrl+Alt+Shift+↑ / ↓**。
完整按键、设置与排错见 [README](../README.md)。

**日本語** — 終了：**Esc を素早く2回**。拡大縮小：
**Ctrl+Alt+Shift+↑ / ↓**。全キー・設定・トラブルは
[README](../README.md) 参照。
