# Workflow: クリーンビルド (C#/.NET PC側アプリ)

## 目的 (Objective)
スマホVR向けデスクトップ画面拡張アプリの **Windows PC側 C#/.NET プロジェクト** を、
再現性のある形でクリーンビルドする。前回ビルドの成果物に依存しない状態から
`clean → restore → build` を実行し、コンパイルが通ることを保証する。

## 必要な入力 (Required Inputs)
- ビルド対象の `.sln` または `.csproj`
  - リポジトリ直下〜サブフォルダに1つだけなら **自動検出**される
  - 複数ある／別の物を指定したい場合は `--target <相対パス>` を渡す
- ビルド構成: `Debug`（既定）または `Release` — `--config` で指定
- 前提: `dotnet` CLI（.NET SDK）が PATH 上にあること

## 使うツール (Tool)
`tools/clean_build.py` — 決定論的に以下を順に実行する:
1. `dotnet clean`   … 既存の出力を削除
2. `dotnet restore` … 依存パッケージを取得
3. `dotnet build --no-restore` … コンパイル

実行例（このマシンでは `python` ではなく `py -3` を使う。下の学習メモ参照）:
```
py -3 .\tools\clean_build.py
py -3 .\tools\clean_build.py --config Release
py -3 .\tools\clean_build.py --target src\PcAgent\PcAgent.csproj
```

## 期待される出力 (Expected Outputs)
- 標準出力にビルドログをストリーム表示
- 完全なログを `.tmp/build_<日時>.log` に保存（成果物は中間物。`.tmp/` は破棄可）
- 成功時: `BUILD SUCCEEDED (<config>)` を表示し終了コード `0`

## エッジケースと対処 (Edge Cases)
- **`.sln`/`.csproj` が見つからない** → 終了コード2。まだ.NETプロジェクトが
  無いということ。プロジェクト作成を先に行う（ユーザーに確認）。
- **`.sln`/`.csproj` が複数** → 終了コード2でファイル一覧を表示。
  `--target` で対象を明示する。
- **`dotnet` が無い** → 終了コード2。.NET SDK のインストールが必要。
- **コンパイルエラー** → 終了コード1。`.tmp/build_<日時>.log` の
  全文を読み、原因を特定 → 修正 → 再実行（無料ローカル処理なので再実行可）。

## 学習メモ (Learnings)
<!-- ビルドで判明した制約・癖・再発トラブルをここに追記して育てる -->
- **Python起動**: このマシンの `python` は Microsoft Store のスタブで動作しない
  （"not a valid application for this OS platform"）。tools/ は必ず `py -3`
  ランチャーで実行する（Python 3.10.10 を確認済み）。
- **.NET SDK 導入済み**: 2026-05-16 に winget で **.NET 8 SDK 8.0.421** を
  `C:\Program Files\dotnet\` に導入済み。
- **PATH 未反映の癖**: winget でのインストール直後、既に開いていたシェルには
  `dotnet` の PATH が反映されない（新規シェルで有効）。このため WAT ツールは
  `tools/_dotnet.py` の `find_dotnet()` で **PATH→標準インストール先の順**で
  `dotnet` を解決する（`clean_build.py`/`scaffold_project.py`/`run_app.py`
  共通）。既存セッションからでも動く。
- ツールのスモークテスト済み: `--help` 正常、プロジェクト/SDK 不在時は
  終了コード2＋明確なメッセージで安全に停止することを確認。
