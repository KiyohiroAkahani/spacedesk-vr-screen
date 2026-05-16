# Workflow: .NETプロジェクト雛形生成 (Solution + WPF Desktop App)

## 目的 (Objective)
PC側アプリ（デスクトップをキャプチャし、横並び2枚＝Side-By-Side で表示して
spacedesk 経由で iPhone/VR に出す **GUIアプリ**）の骨組みを決定論的に生成する。
当初 Worker Service で雛形化したが、Worker Service は **GUI を持たず**
可視ウィンドウを出せないため本用途に不適。**WPF** テンプレートでソリューションと
メインプロジェクトを作る（既定 `wpf`。WinUI3 も選択可、将来余地）。

## 必要な入力 (Required Inputs)
- プロジェクト/ソリューション名（PascalCase）: 既定 `VrDesktopBridge` — `--name`
- テンプレート: `wpf`(既定) / `winui` / `worker` — `--template`
- ターゲットフレームワーク: 既定 `net8.0` — `--framework`
  - **wpf/winui の場合、素の `net8.0` は自動で `net8.0-windows10.0.19041.0`
    に書き換わる**（WinRT `Windows.Graphics.Capture` 投影に必要）
- 前提: `dotnet` CLI が利用可能なこと。ツールは PATH→標準インストール先の順で
  探すので、winget 直後で PATH 未反映のセッションでも動く（→ `build.md` 学習メモ）

## 使うツール (Tool)
`tools/scaffold_project.py` — 以下を決定論的に実行:
1. `dotnet new sln -n <Name>`                         … ソリューション作成
2. `dotnet new wpf -n <Name> -o src/<Name> -f <fw>`   … WPFプロジェクト
3. `dotnet sln <Name>.sln add src/<Name>/<Name>.csproj` … sln へ追加
4. `dotnet add <csproj> package …`（worker以外）       … 必要NuGetを固定
   - `Vortice.Direct3D11`, `Vortice.DXGI`, `Vortice.D3DCompiler`,
     `Microsoft.Windows.CsWin32`

実行例（このマシンでは `py -3`。`workflows/build.md` 学習メモ参照）:
```
py -3 .\tools\scaffold_project.py
py -3 .\tools\scaffold_project.py --name VrDesktopBridge --template wpf
```

## 期待される出力 (Expected Outputs)
- `<Name>.sln`（リポジトリ直下）
- `src/<Name>/<Name>.csproj` ＋ WPF テンプレート一式（`App.xaml`,
  `MainWindow.xaml`, `AssemblyInfo` 等）、TFM = `net8.0-windows10.0.19041.0`
- 上記 NuGet 参照が固定済み（ビルド可能な状態）
- プロジェクトが sln に追加済み
- 成功時: `SCAFFOLD CREATED` を表示し終了コード `0`。続けて
  `tools/clean_build.py` でビルド検証、`tools/run_app.py` で起動

## エッジケースと対処 (Edge Cases)
- **`.sln`/プロジェクトが既に存在** → 終了コード3で対象を表示し中断。
  既存を壊さない設計。意図的に上書きする場合のみ `--force`。
- **`dotnet` が無い** → 終了コード2。.NET SDK インストールが先。
- **`--template winui` だがテンプレ未導入** → 終了コード2。
  `dotnet new install Microsoft.WindowsAppSDK.ProjectTemplates` を案内
  （自動導入はしない＝システム変更は確認の上）。
- **不正な `--name`**（記号・先頭数字など）→ 終了コード2でメッセージ表示。
- **dotnetステップ失敗** → 終了コード1。出力を読み原因特定 → 修正 → 再実行。

## 想定スコープ (Scope)
ユーザー方針により **ソリューション＋メインプロジェクトのみ**。
テストプロジェクト・`.editorconfig`・`global.json`・README 雛形は未採用
（将来このワークフロー／ツールに追加する候補）。

## 学習メモ (Learnings)
<!-- 生成で判明した制約・癖・再発トラブルをここに追記して育てる -->
- このマシンの Python は `py -3` で実行（共通の癖。`workflows/build.md` 参照）。
- **Worker→WPF 転換**: 当初 Worker Service で雛形化したが GUI 不可のため WPF へ。
  `--template` を残し worker/winui も選べる形に。
- **WGC用TFM要件**: WPF からでも `Windows.Graphics.Capture` を使うため TFM は
  `net8.0-windows10.0.19041.0` 必須。素の `net8.0` 指定時はツールが自動書換。
- CsWin32 は `NativeMethods.txt`（生成API一覧）が無いと P/Invoke を生成しない。
  雛形生成後、実装(M1)で `src/<Name>/NativeMethods.txt` を作ること。
- 実生成の検証結果はここに追記する（SDK導入済み・8.0.421）。
