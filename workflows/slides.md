# Workflow: セットアップ説明スライド生成 (Slides)

## 目的 (Objective)
`SETUP_GUIDE.md` の内容に沿った、配布・プレゼン用の
**`dist\SETUP_GUIDE.pptx`**（英中日併記・初心者向け・フェード切替付き・
全11スライド）を、コマンド一発で再生成する。SETUP手順を更新したら
作り直す。

## 必要な入力 (Required Inputs)
- なし（内容は `tools/make_slides.py` 内に保持。手順変更時はこのスクリプト
  のテキストを編集して再実行）
- 前提: `py -3`（このマシンの癖。`build.md` 学習メモ参照）。
  `python-pptx` は未導入なら初回に自動 pip インストールを試行する

## 使うツール (Tool)
`tools/make_slides.py`:
```
py -3 .\tools\make_slides.py
```
生成物: `dist\SETUP_GUIDE.pptx`（既存があれば上書き）。

## スライド構成 (11枚)
1. タイトル / 2. 必要なもの(0) / 3. 導入(1: spacedesk 2点・Store版禁止) /
4. Wi-Fi＋exe入手＋SmartScreen(2) / 5. 接続(3a) /
6. **ディスプレイ「拡張」=最重要(3b・赤)** / 7. ゴーグル＋起動(4) /
8. 操作(5) / 9. **おすすめ:4K動画(6・YouTube例)** / 10. トラブル(?) /
11. リンク＆謝辞

## 期待される出力 (Expected Outputs)
- `dist\SETUP_GUIDE.pptx`（16:9、各スライドにフェードのスライド切替）
- 再オープン検証OK（python-pptxで読み直して妥当性確認）
- 成功時 `SLIDES CREATED: ...`（スライド数・バイト数）＋終了コード0

## エッジケースと対処 (Edge Cases)
- `python-pptx` 不在＆自動インストール失敗 → 終了コード2。
  `py -3 -m pip install python-pptx` を手動実行
- 生成例外 → 終了コード1。`.tmp\slides_<日時>.log` を確認
- pptx を開いたまま再実行するとロックで保存失敗 → PowerPointを閉じる

## 配布メモ (Distribution)
- `dist/` は `.gitignore` 済み（exe同様、pptxはソースから再生成可能の
  ため git には入れない）。配布は **GitHub Release に添付** が基本。
- 要素ごとのビルドアニメは PowerPoint の「アニメーション」タブで付与
  （開始=直前の動作の後）。本ツールはスライド切替フェードまで自動付与。

## 学習メモ (Learnings)
- 文言は `SETUP_GUIDE.md` と整合させること（特に Step3「表示画面を
  拡張する」/ Step6 4K動画リンク）。手順を変えたら本スクリプトも更新。
- PowerPoint のビルドアニメ XML 自動付与はファイル破損リスクが高いため
  非採用。確実なフェード切替＋視覚デザインで分かりやすさを担保。
