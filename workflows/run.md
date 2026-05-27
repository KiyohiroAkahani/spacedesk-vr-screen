# Workflow: アプリ起動 (WPF SBSミラー)

## 目的 (Objective)
ビルド済みの WPF アプリを起動し、spacedesk の拡張モニタ上で
デスクトップの横並び2枚 (Side-By-Side) 表示を確認する。

## 必要な入力 (Required Inputs)
- 起動対象の `.csproj`（リポジトリ内に1つなら自動検出。複数なら `--target`）
  - 注: `dotnet run` は `.sln` 不可。必ず `.csproj` を解決する
- ビルド構成: `Debug`(既定) / `Release` — `--config`
- 再ビルドskip: `--no-build`（直前にビルド済みで素早く再起動したいとき）
- 前提:
  - `tools/clean_build.py` が成功していること
  - 動作確認するなら spacedesk で iPhone を **EXTEND（拡張）モード**接続し、
    Windows 表示設定に拡張ディスプレイが出ていること
  - `dotnet` は PATH→標準インストール先の順で解決（→ `build.md` 学習メモ）

## 使うツール (Tool)
`tools/run_app.py` — `dotnet run --project <csproj> -c <config>` を実行。

実行例（このマシンでは `py -3`）:
```
py -3 .\tools\run_app.py
py -3 .\tools\run_app.py --config Release
py -3 .\tools\run_app.py --no-build
```
**注**: GUIアプリのため、ウィンドウを閉じる（Esc）までフォアグラウンドで
ブロックする。これはハングではなく仕様。

## 期待される出力 (Expected Outputs)
- 別ウィンドウでアプリ起動（M2以降は対象モニタへ全画面 SBS 表示）
- 標準出力にログをストリーム、全文を `.tmp/run_<日時>.log` に保存
- Esc で正常終了し終了コード `0`（`APP EXITED cleanly`）

## エッジケースと対処 (Edge Cases)
- **`dotnet` が無い** → 終了コード2。.NET SDK 導入が先。
- **`.csproj` が無い** → 終了コード2。先に `scaffold_project.py` で雛形生成。
- **`.csproj` が複数** → 終了コード2で一覧表示。`--target` で明示。
- **`--target` が .sln** → 終了コード2（run は csproj のみ）。
- **実行時例外/ビルド失敗** → 終了コード1。`.tmp/run_<日時>.log` 全文を
  読み原因特定 → 修正 → 再実行。
- **spacedesk 未接続/対象モニタ無し** → アプリはプライマリへフォールバックし
  警告ログ（M3 以降の挙動。詳細は実装時にここへ追記）。

## 学習メモ (Learnings)
<!-- 起動・運用で判明した制約・癖・再発トラブルをここに追記して育てる -->
- このマシンの Python は `py -3` で実行（共通の癖。`build.md` 参照）。
- 実行ブロックの仕様（Esc 終了まで前面）を運用者に周知すること。
- **キャプチャ方式の決定**: 計画では WGC だったが、単一GPUで1モニタ全体を
  ミラーする用途では WGC の利点（クロスGPU/ウィンドウ単位）が不要で、
  WGC は黄色キャプチャ枠が出る・WinRT `IGraphicsCaptureItemInterop` の
  COM相互運用が脆い。よって **DXGI Desktop Duplication（Vortice）** を採用。
  枠無し・相互運用不要。制約: キャプチャ用D3D11デバイスは出力と同一GPU
  （本機は1台なので問題なし）。
- **Vortice 3.6.2 の正確なAPI**（推測厳禁。`.tmp/probe` 反射プローブで確定）:
  - `D3D11.D3D11CreateDevice(IDXGIAdapter adapter, DriverType, DeviceCreationFlags,
    FeatureLevel[] featureLevels, out ID3D11Device, out ID3D11DeviceContext)`
    — adapter は `null!`、DriverType.Hardware、featureLevels 配列必須。
  - `IDXGIAdapter.EnumOutputs(uint, out IDXGIOutput)`（`GetOutput` は無い）。
  - `Compiler.Compile(src, entry, name, profile)` は **`ReadOnlyMemory<byte>`**
    を返す（`Blob` ではない）。`CreateVertexShader(mem.Span)`。
  - `AcquireNextFrame(uint timeoutMs, ...)`（int 不可）。
  - タイムアウト判定は `Vortice.DXGI.ResultCode.WaitTimeout`。
- **API確定手法**: 不明なVortice/ネイティブAPIは `.tmp/probe`（使い捨て）に
  最小 csproj+リフレクションプログラムを作り `dotnet run` で列挙→確定後に削除。
  PowerShell 5.1 は net8 アセンブリを反射できないので不可。
- M1 検証: `clean_build.py` 緑、exe を8秒タイムボックス起動して stderr 無し
  ＝レンダラ（device/swapchain/duplication/shader）初期化成功を確認。
- **GUI起動の検証手法**（run_app.py はブロックするので非対話環境では使えない）:
  `Start-Process exe -PassThru -RedirectStandardError`→`Start-Sleep 8`→
  `HasExited`/`CloseMainWindow`/`Kill`。stderr 無し＝起動クラッシュ無し。
- M2/M3 検証済み: MonitorPicker は本機を `\\.\DISPLAY1 "Generic PnP Monitor"
  <解像度> primary` と列挙。spacedesk 未接続時は target がプライマリに
  フォールバック→再帰警告＋`ExcludeFromCapture=True` 自動適用、
  `SetWindowDisplayAffinity` は本 Win11(10.0.26200) で "applied" 成功。
- **本機は現状シングルモニタ（<解像度>）・spacedesk 未接続**。実VR確認には
  spacedesk を EXTEND モードで接続し、`config.json` の `TargetMonitorMatch`
  を "spacedesk"（既定）にして起動、ウィンドウが拡張ディスプレイへ載る。
- **M5 操作透過＋アスペクト保持（実装済・ビルド緑・起動検証済）**:
  - ミラー窓に `WS_EX_NOACTIVATE|WS_EX_TRANSPARENT|WS_EX_TOOLWINDOW` ＋
    `ShowActivated=False`＋`ShowInTaskbar=False`。フォーカス/マウスを奪わず
    プライマリ作業を妨げない。`WS_EX_LAYERED` は**意図的に不使用**（レイヤード
    トップ窓は D3D 子HWND を載せられず黒画面化するため。透過は TRANSPARENT
    単独でヒットテスト透過が成立）。
  - 入力透過で窓内キー入力不可 → **グローバルホットキー**へ置換:
    終了 `Ctrl+Alt+Shift+Q` / 対象モニタ巡回 `+F` / WDA切替 `+A`
    （`RegisterHotKey`＋`HwndSource` の `WM_HOTKEY` フック）。
    端末で `Ctrl+C` でも終了可（保険）。旧 Esc/F/A 窓内ハンドラは保険で残置。
  - SBS は各眼を `Fit()`(contain) でデスクトップ縦横比保持・中央描画。
    余白は黒帯（レターボックス/ピラーボックス）。引き伸ばし無し。
- 旧ホットキー（窓フォーカス時のみ有効・保険）: Esc/F/A。
- **カーソル合成（重要修正）**: DXGI Desktop Duplication は**マウスカーソルを
  画面に合成しない**（カーソルは別データ）。そのためミラーにカーソルが映らず
  ヘッドセット越しに操作不能だった。`Capture/CursorOverlay.cs` で
  `GetCursorInfo`/`GetIconInfo`/`GetDIBits` から現在カーソルをBGRA化
  （カラー＝埋込αまたはANDマスク、モノクロ＝AND/XOR合成）、
  `SbsRenderer` が各眼の Fit 矩形内の正しい位置（`OutputX/Y` で
  キャプチャ元モニタ相対に変換、ホットスポット補正）にαブレンドで重ね描画。
  形状変更時のみテクスチャ再生成（`ShapeVersion`）。
  ※マウス/キーボード自体は元々プライマリに効いていた。本質は「見えない」
  ことだったので、見えるようにするのが修正の要点。
- Vortice 3.6.2 追加API（probeで確定）: `CreateTexture2D(ref desc,
  SubresourceData)`、`SubresourceData(IntPtr,uint rowPitch,uint slice)`、
  `CreateBlendState(new BlendDescription(Blend.SourceAlpha,
  Blend.InverseSourceAlpha))`、`OMSetBlendState(state)`（null可で無効化）。
- **クリック/キーボードが効かない問題の本質と対策**: ミラーは映像であり
  実体ではない。物理マウスはプライマリに効くが、ヘッドセット装着中に
  カーソルが空のspacedesk拡張デスクトップへ逸れて空クリックになっていた。
  対策＝`ClipCursor` で **キャプチャ元(プライマリ)モニタにマウスを拘束**
  （`AppConfig.ConfineCursor` 既定true、`Ctrl+Alt+Shift+C` トグル、
  `DispatcherTimer` 750ms で再アサート、終了時 `ClipCursorClear`）。
  これで「ミラーに見えるカーソルどおりに実ウィンドウをクリック→フォーカス
  →キーボード入力」が成立。
- **Ctrl+Alt+Del 後に複製が止まる対策**: セキュアデスクトップで DXGI
  Desktop Duplication がアクセス喪失。`DesktopDuplicator` を強化し、
  acquire 失敗 Result / `_dupl==null` でも 500ms スロットルで
  `DuplicateOutput` 自動再構築（復帰後ミラー再開）。
- **exe ロックの癖（重要・再発防止済み）**: アプリ起動中に `clean_build.py`
  /`dotnet run` すると MSB3021/3027（`VrDesktopBridge.exe` 使用中）。
  → 旧インスタンスが残ると **新コードが反映されず「修正しても変化なし」**
  という症状になる。対策として:
  - 新ツール `tools/stop_app.py`（共有 `tools/_proc.py` の `kill_app()` =
    `taskkill /F /T /IM VrDesktopBridge.exe`）。**確実な停止スイッチ**。
    ウィンドウが透過/非アクティブでホットキー不達でも必ず止まる。
  - `run_app.py` は**起動前に残存インスタンスを自動kill**し、**Ctrl+C で
    アプリも巻き込んで確実終了**するよう改修。これで毎回フレッシュな
    ビルドが動く。
  - 停止手段（どれでも）: 端末 `Ctrl+C` / `py -3 .\tools\stop_app.py` /
    `Ctrl+Alt+Shift+Q`。スモークテスト後も `stop_app.py` で必ず後始末。
- 「全部変化なし」の最有力原因＝**古いインスタンス残存でリビルド未反映**。
  まず `stop_app.py` → `run_app.py` で再現確認すること。
- **核心バグ（クリック/キーボード不達の真因）**: ユーザーは Bluetooth
  マウスで操作。ヘッドセット＝spacedesk拡張モニタを見ているため、実OS
  カーソルが**拡張モニタ上をさまよう**（透過ウィンドウの裏＝空デスクトップ
  →クリックが何にも当たらない／フォーカス取れずキーボードも不達）。
  症状: ヘッドセット内にカーソル3つ（合成2＋実1が最前面）。
- **`ClipCursor` は非フォアグラウンド窓では OS が無視/解除**するため、
  本アプリ（`WS_EX_NOACTIVATE`）の `ClipCursor` だけでは拘束不能。
  → 対策＝**毎フレーム `GetCursorPos`/`SetCursorPos` で実カーソルを
  キャプチャ元(プライマリ)へハードクランプ**（`MainWindow.ClampCursorToCapture`、
  FPSキャップより前で毎tick実行、フォーカス非依存で確実）。`ClipCursor`
  は補助として併用。`Ctrl+Alt+Shift+C` で拘束ON/OFF（OFF時はクランプ停止）。
  これで「ヘッドセット内の合成カーソル＝実カーソル」が一致し、見た位置の
  実ウィンドウにクリック/入力が通る。実カーソルはプライマリ外に出ない
  ので拡張モニタ上の“3つ目”は消える。
- **描画ループ非依存のクランプ＋診断**（クランプが効かない時の切り分け）:
  `CompositionTarget.Rendering` は副/被覆モニタ上の窓で間引かれることが
  あるため、クランプは **`System.Timers.Timer` 8ms（スレッドプール、
  フォーカス/描画非依存）** を主駆動にした（OnRendering 側は保険）。
  1Hz で stderr に `[DIAG] renderFps=.. confine=.. cap=(x,y,wxh)
  clampHits=.. cursor=(px,py)->(nx,ny)` を出力。実機で「変化なし」時は
  この行で原因切り分け: renderFps=0→描画停止 / cap がプライマリでない→
  MonitorPicker誤検出 / clampHits増えるのにカーソル戻らない→SetCursorPos
  が上書き(要 低レベルフック) / clampHits=0 のまま拡張へ→ cap 矩形誤り。
  ログは `.tmp/run_<日時>.log` と端末に出る。
- **実機DIAG解析（2026-05-16）**: ユーザー実行ログは `[MON] 1 monitor`,
  `cap=(0,0,<解像度>)`, 全カーソル座標がその矩形内, `clampHits=0`,
  `Target monitor is PRIMARY` フォールバック。＝**spacedesk が拡張モニタ
  として列挙されていない**（未接続/Duplicateモード、または起動時1回きり
  列挙の後に接続）。クランプは正しく動くが拡張モニタが無いので無意味。
- **対策**: 起動時に全モニタを `[MON]` ダンプ。`WM_DISPLAYCHANGE` と
  `Ctrl+Alt+Shift+D`(再検出) で**動的に再列挙→再ターゲット→再配置**
  （起動後にspacedesk接続でもOK＝一回きり列挙バグも解消）。
- **要確認**: spacedesk は必ず **EXTEND（拡張）モード**で、Windows 設定>
  ディスプレイに**2台目が見える**こと。Duplicate/ミラーだと2台目が
  増えず本アプリの前提が崩れる。実機 `[MON]` 行で2台目の有無・名称・
  座標を確認してから次の手を決める。
- **複製(Duplicate)モード対応（確定方針）**: ユーザーは spacedesk を
  「表示画面を複製」で使用（Windows設定で 1|2 重なり＝モニタは論理1台）。
  Extend は使わない方針。複製でも SBS 表示自体は spacedesk が我々の窓を
  映すので成立済み（ユーザー確認済み）。**唯一の障害は実HWカーソル**：
  素の座標で描画されSBSのスケール表示と一致せず狙えない（3つ目の原因）。
  → 対策: `_monitors.Count<=1`(=複製/単一)を `_duplicateMode` とし、
  `Capture/CursorHider`(SetSystemCursor透明→SPI_SETCURSORSで復元)で
  **実OSカーソルを全体非表示**、`CursorOverlay.ForceStandardArrow`/
  `SbsRenderer.UseStandardArrow` で固定矢印を内容正位置に合成描画。
  複製モードでは拘束(ClipCursor/clamp)は無意味なので自動OFF。
  クリックは実ポインタ位置＝合成矢印と同じ Fit 変換なので一致して当たる。
- **カーソル復元の安全策（重要）**: 隠したまま終了するとOS全体でマウスが
  見えなくなるため、全終了経路で復元: `MainWindow.OnClosed` /
  `AppDomain.ProcessExit`/`UnhandledException` / `App` の
  `DispatcherUnhandledException`(SPI_SETCURSORS) / 強制kill対策として
  `tools/_proc.py kill_app()` が finally で `restore_cursors()`
  （`stop_app.py`・`run_app.py` 経由で必ず復元）。
- 手動トグル: `Ctrl+Alt+Shift+H` で実カーソル非表示ON/OFF（Extend環境で
  試したい時や、複製判定が外れた時の保険）。`H` キー(窓フォーカス時)も可。
- **真の原因＝クリックが下に通っていなかった（確定）**: 症状「クリック/
  入力欄/キーボード無反応・Ctrl+Alt+Delのみ可」。複製モードでは全画面の
  ミラー窓が実デスクトップを覆い、**クリックを食べていた**。
  `WS_EX_TRANSPARENT` 単体はクリックスルー不確実（`WS_EX_LAYERED` 併用が
  必要だが D3D 子窓が黒化するため不可）。→ **`WM_NCHITTEST` で
  `HTTRANSPARENT(-1)` を返す**ことで、レイヤード無し・描画維持のまま
  全クリックを下の実ウィンドウへ確実に透過（`MainWindow.HotkeyHook`
  先頭で処理、両モード常時有効）。これでクリック→フォーカス→キーボードが
  実アプリに届く。Extendで問題化しなかったのは窓が別モニタだったため。
- 残: 「3つ目のカーソル」は spacedesk クライアントが自前描画する
  ポインタの可能性が高く（OSカーソル非表示でも残る）アプリ側で完全制御
  不可。狙いは**合成矢印**で行う運用とする。気になる場合は spacedesk
  設定のカーソル表示オプションを確認。
- **方針転換（ユーザー決定）**: 複製モードでの操作改善は技術的に困難
  （映像のクリック透過と座標整合が根本的に両立しない）と判断。以降は
  **拡張(Extend)モード前提**で開発。複製モード向けの暫定対応コードは
  残置（無害）だが今後の主対象ではない。
- **M4 VRレンズ歪み補正 実装済み（2026-05-16）**: `SbsRenderer` を
  2パス化。Pass1=SBS(デスクトップ+カーソル)をオフスクリーンRT
  (`_sceneTex`)へ描画、Pass2=眼ごとに**樽型歪み補正シェーダー `PSD`**
  (`1+k1*r2+k2*r2^2`、定数バッファ `DistortCB` 32B)でバックバッファへ
  リゾルブ。`AppConfig.LensDistortion`(既定true)/`DistortK1`(0.22)/
  `DistortK2`(0.10)、`config.json` で調整可。`Ctrl+Alt+Shift+V` で
  ON/OFFトグル。カーソルもPass1に含むので歪みと整合。
  Vortice API は probe で確定: `CreateBuffer(ref BufferDescription,
  IntPtr)`、`UpdateSubresource(ref T,res,0,0,0,null)`、
  `PSSetConstantBuffer(0,buf)`、`BufferDescription(byteWidth,BindFlags,
  Usage,Cpu,Misc,stride)`、`BindFlags.RenderTarget|ShaderResource`。
  k1/k2 は Cardboard レンズ依存なので実機で要調整（V でOFF比較）。
  実機検証で歪み補正ONは合格（OFFはVRとして見づらい）＝既定ON妥当。
- **機能追加: Escダブルタップで終了（ユーザーフレンドリー終了）**:
  窓はクリック透過/非アクティブで通常KeyDownのEscが届かず、Escを
  グローバルホットキー化すると全アプリのEscを奪うため不可。
  → `Interop/GlobalEscHook`（`WH_KEYBOARD_LL`）で **Escを“消費せず”観測**し、
  **0.5秒以内に2回**で `Close()`。単発Escは他アプリで通常動作・誤終了防止。
  終了手段一覧: Escダブルタップ / `Ctrl+Alt+Shift+Q` / 端末Ctrl+C /
  `py -3 .\tools\stop_app.py`。フックはUIスレッドでInstall、OnClosedで
  Uninstall。`Stopwatch.GetTimestamp` で時刻計測（DateTime不要）。
  **完全終了に統一**: Escダブルタップ / Ctrl+Alt+Shift+Q / 窓Esc は
  すべて `MainWindow.QuitHard()` ＝ `Close()`(OnClosed後始末:カーソル/
  クリップ復元・フック解除・ホットキー解除・renderer破棄)→
  `Environment.Exit(0)` でプロセス強制終了（端末Ctrl+Cと同等。タイマー/
  フック/`dotnet run` が残らない）。`_quitting` 再入防止。
- **機能追加: 画面縮尺の段階切替**: `SbsRenderer.ContentScale`（既定1.0）。
  Pass1 の各眼 Fit ビューポートを中心基準で `ContentScale` 倍に縮小
  （desktop/cursor とも eyes[] 共有なので整合）。`Ctrl+Alt+Shift+S`
  （窓フォーカス時は `S`）で **100→90→80→70→100…** をループ
  （`ScaleSteps`）。ログ `[INFO] ScreenScale=NN%`。縮小分は黒縁。
  追加: **`Ctrl+Alt+Shift+↑＝拡大 / ↓＝縮小`**（10%刻み・50〜150%、
  `ScaleBy`）。スマホタップでは変わらない（ホットキー操作。タップは
  spacedeskタッチでアプリ無関係）と周知すること。plainCtrl+矢印は
  他アプリと衝突するため Ctrl+Alt+Shift+矢印 を採用。
- **機能: 初回操作ガイド**: 各眼の左上 `End [Esc → Esc]` / 右上
  `Zoom [Ctrl+Alt+Shift+↑/↓]` を半透明パネルで表示。`Render/GuideOverlay`
  が WPF `RenderTargetBitmap`(Pbgra32→un-premultiply で straight BGRA)
  で2枚生成→`SbsRenderer` が Pass1 で各眼にαブレンド描画（歪み補正も
  かかる）。`SbsRenderer.ShowGuide`、`MainWindow.BumpGuide()` が起動時表示
  ＋3秒アイドルで非表示（`_guideTimer` DispatcherTimer、ホットキー/キー
  操作で再表示＆リセット）。テキスト生成は UI スレッド必須
  （renderer は CompositionTarget.Rendering=UIスレッドなので可）。
  デザイン(現行): 白角丸バッジ、**2段組**＝1段目タイトル黒太字
  (END/ZOOM, FontSize110)・2段目キー操作を **spacedesk緑**
  (`[Esc → Esc]`/`[Ctrl+Alt+Shift+↑/↓]`, FontSize84)、**外枠も緑**
  (`SpacedeskGreen=#3FB23F`、`GuideLabel.Build(title,hint)`、要調整可)。表示は
  **7秒保持→約0.9秒フェードアウト**（`PSG` シェーダー＋`GuideCB.Opacity`、
  `SbsRenderer.GuideOpacity`、`MainWindow` の33ms `_guideTimer` で
  `GuideHoldMs=7000`/`GuideFadeMs=900` を補間）。操作で `BumpGuide()`
  が再表示＆タイマーリセット。
  サイズ/位置（改訂）: 約3倍化（FontSize110、`gh=clamp(eye.H*0.15,42,220)`）、
  各眼の **1/4・3/4 幅（中央と端の中間）・上部**（top=eye.Y+H*0.04）に配置、
  端はみ出しは Clamp。
- **EXTEND の致命的欠陥と対策（ウィンドウ迷子）**: ミラーはプライマリ。
  フォルダ等が spacedesk 拡張モニタ側に開くと、ミラーに映らず（拡張側は
  我々の窓が覆う）カーソルも拘束で届かない＝「表示されない/クリック不可/
  ドラッグ不可」。→ `Interop/WindowWrangler` が前面ウィンドウを400ms毎に
  監視し、中心がキャプチャ元(プライマリ)外なら自動でプライマリ内へ
  移動・収容（shell/自窓は除外、同一hwndは1回のみ＝ループ防止）。
  `AppConfig.KeepWindowsOnCapture` 既定true（複製/単一モニタ時は無効化）、
  `Ctrl+Alt+Shift+W` トグル。`[INFO] Moved stray window ...` ログで
  実際に迷子が起きていたか確認できる（原因切り分け兼用）。
- **機能: IPD（水平レンズ中心）調整**: ユーザー報告「縮尺70%程度で両眼
  同時にピントが合わない（片眼ずつ合わせても両眼にすると片方がずれる）」。
  原因はSBSの**左右像中心が常に画面幅の半分**で、典型スマホでは~75mm＞
  成人平均IPD~63mm のため両眼が外向きに発散して輻輳破綻。対策＝
  `SbsRenderer.IpdShift`（眼ハーフ幅に対する比率、-15%〜+15%）で
  **Pass 1の各眼Fit位置と Pass 2の `DistortCB.Cx` を協調的に**内側／外側
  へシフト（両者を必ず一致させる。ずれると更に悪化）。Pass 1のシフト後は
  左目を `[0, half]`／右目を `[half, _width]` 内に Clamp（隣接眼の内容を
  サンプルしないため）。`AppConfig.IpdShiftPercent`（既定 **0 = 既存
  互換**）。**`Ctrl+Alt+Shift+→`（Narrower=狭める, +1%）/`←`（Wider=広げる,
  -1%）**でリアルタイム調整、ログ `[INFO] IpdShift=NN%`。決まった値は
  config.json の `IpdShiftPercent` で永続化。ContentScale 非依存
  （`dx = IpdShift * half` は scene 全幅基準）。各眼上部に **END /
  CENTER / ZOOM** の3バッジで操作キーをガイド表示。
- 起動時既定縮尺 = **80%**（`AppConfig.StartupScalePercent` 既定80、
  50-150でクランプ、`config.json` で変更可）。初期 `_scaleIdx=-1` で
  Up/Down はこの起点から増減。起動ログ `ScreenScale=90% (startup)`。
- `config.json` は初回起動時 `%LOCALAPPDATA%\VrDesktopBridge\config.json`
  に既定値で生成。`fps`(15-120,既定60)/`TargetMonitorMatch`/`ExcludeFromCapture` 等。
- 未実装(将来): キャプチャ元の明示選択（現状 DXGI 出力0＝プライマリ固定）、
  M4 樽形歪み補正シェーダー（v1対象外・ユーザー合意済み）。
