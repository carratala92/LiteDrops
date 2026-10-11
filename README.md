<p align="center"><img src="assets/icon.png" width="96" alt="LiteDrops icon"></p>

<h1 align="center">LiteDrops</h1>

<p align="center">Lightweight Twitch Drops miner for Windows — mines only the games you pick.<br>
選んだゲームだけを掘る、軽量な Twitch Drops マイナー（Windows 用）</p>

---

## 日本語

### 特徴
- **軽い**：約 500 KB の exe 1 つ。メモリはウィンドウ表示中で約 20 MB、トレイ格納中はさらに解放します
- **選んだゲームだけ**：チェックを入れたゲームを上から順に掘ります
- **ドラッグで並べ替え**：左のつまみ（⋮⋮）をドラッグして優先順位を変更、× で削除（削除は「元に戻す」で取り消せます）
- **ゲーム検索**：ゲーム名を入力すると Twitch 上の正式名が候補に出るので、選ぶだけで追加できます
- **日本語 / 英語 UI**：Windows の表示言語に合わせて自動で切り替わります（トレイメニューから変更も可）
- **Windows 起動時に開始**：トレイメニューから設定でき、起動時はトレイに入った状態で採掘を始めます
- **自動で受け取り**：完了したドロップは自動で受け取ります（Claim）
- **映像はダウンロードしない**：配信のプレイリストだけを取得して視聴扱いにするので、通信量はごくわずかです
- **ログイン情報は暗号化して保存**：Windows の DPAPI で暗号化するため、他の PC や他のユーザーでは使えません

### 動作環境
- Windows 10 / 11（64bit）
- [.NET 10 デスクトップ ランタイム](https://dotnet.microsoft.com/download/dotnet/10.0)

### 使い方
1. [Releases](../../releases) から `LiteDrops.exe` をダウンロードし、好きなフォルダに置いて起動します
2. 「Twitch でログイン」を押します。ブラウザで Twitch の認証ページが開くので、表示されたコードを入力して許可します
3. 検索欄にゲーム名を入力し、候補から選んで追加します
4. チェックの入ったゲームのキャンペーンを自動で探して、視聴を始めます

- 上にあるゲームほど優先されます。行をドラッグして並べ替え、× で削除できます
- キーボード操作：Alt+↑/↓ で移動、Delete で削除、Space でオン/オフ
- 視聴中のチャンネル名をクリックすると、その配信をブラウザで開きます
- ウィンドウを閉じるとトレイに格納されます。終了するときは、トレイアイコンを右クリックして「終了」を選びます
- 設定（`litedrops.json`）とログ（`litedrops.log`）は exe と同じフォルダに作られます

### 注意
- キャンペーンは、Drops が有効な配信中のチャンネルを探して見つけます。配信者がいないキャンペーンは見つかりません
- 報酬を受け取るには、事前にゲームのアカウントを Twitch と連携しておいてください
- 本ソフトは Twitch 非公式です。自動視聴は Twitch の利用規約に抵触する可能性があります。**自己責任で**使ってください

---

## English

### Features
- **Tiny**: a single ~500 KB exe, ~20 MB of RAM with the window open and less while in the tray
- **Only your games**: checked games are mined top to bottom
- **Drag & drop priority**: drag the ⋮⋮ handle to reorder, × to remove (with undo)
- **Game search**: type a name and pick Twitch's exact spelling from the suggestions
- **English / Japanese UI**, following the Windows display language (changeable from the tray menu)
- **Start with Windows** option in the tray menu (starts minimized to the tray)
- **Auto-claim** of finished drops
- **No video download**: only stream playlists are fetched, so bandwidth use is minimal
- **Encrypted login**: the token is protected with Windows DPAPI

### Requirements
- Windows 10 / 11 (x64)
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

### Usage
1. Download `LiteDrops.exe` from [Releases](../../releases) and run it
2. Click **Log in with Twitch**, then enter the shown code on the Twitch activation page
3. Add games with the search box (pick a suggestion), then leave it running

Drag rows to change the priority (top = first). Keyboard: Alt+↑/↓ to move, Delete to remove, Space to toggle. Click the channel name to open the stream. Closing the window keeps LiteDrops in the tray; use **Exit** from the tray menu to quit.

### Notes
- Campaigns are discovered through live, drops-enabled channels of your games
- Link your game accounts on Twitch beforehand, or the rewards can't be claimed
- Not affiliated with Twitch. Automated viewing may be against Twitch's Terms of Service — use at your own risk

---

## Build

```
dotnet publish -c Release -o publish
```

## License

[MIT](LICENSE) © 2026 naoki6310
