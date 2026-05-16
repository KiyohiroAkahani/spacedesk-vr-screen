# Workflow: 配布用 自己完結 exe の作成 (Publish)

## 目的 (Objective)
新規ユーザーが **.NET をインストールせずダブルクリックで起動**できる、
自己完結・単一ファイルの `dist\VrDesktopBridge.exe` を生成する。
GitHub Releases / HuggingFace 等での配布物。

## 必要な入力 (Required Inputs)
- ビルド対象 `.csproj`（1つなら自動検出。複数は `--target`）
- ランタイムID: 既定 `win-x64`（`--rid`）
- 前提: ソースからの publish には .NET 8 SDK が必要（`dotnet` は PATH→
  標準インストール先の順で解決＝`build.md` 学習メモ）

## 使うツール (Tool)
`tools/publish_app.py`:
`dotnet publish -c Release -r win-x64 --self-contained true
-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
-p:EnableCompressionInSingleFile=true -o dist`

実行例:
```
py -3 .\tools\publish_app.py
py -3 .\tools\publish_app.py --rid win-x64
```

## 期待される出力 (Expected Outputs)
- `dist\VrDesktopBridge.exe`（単一・自己完結。.NET不要で起動）
- ログ `.tmp\publish_<日時>.log`
- 成功時 `PUBLISH SUCCEEDED` ＋終了コード0
- 個人パスは含まれない。設定は初回起動時 `%LOCALAPPDATA%\VrDesktopBridge\`
  に自動生成され、実行ユーザー環境に自動適合

## エッジケースと対処 (Edge Cases)
- `dotnet` 無し → 終了コード2（SDK導入が必要）
- `.csproj` 無し/複数 → 終了コード2（`--target` 指定）
- publish 失敗 → 終了コード1。`.tmp\publish_<日時>.log` を確認
- exe 実行中だとロックで失敗 → 先に `tools\stop_app.py`

## 配布手順メモ (Distribution)
- `dist\VrDesktopBridge.exe` を GitHub Release / HuggingFace に添付
- リポジトリには `dist/` を含めない（`.gitignore` 済み）。ソース＋
  `start.bat`＋README で「clone してダブルクリック」も可能

## 学習メモ (Learnings)
- 自己完結単一ファイルは初回起動時に一時展開するため初回のみ起動が遅い。
- WPF/Vortice は win-x64 自己完結で動作（ネイティブはOS同梱のd3d11/dxgi）。
- exe ロック時の `stop_app.py` 併用は build.md/run.md と同様。
