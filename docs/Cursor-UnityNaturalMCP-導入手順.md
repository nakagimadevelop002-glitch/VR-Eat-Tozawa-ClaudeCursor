# Cursor で UnityNaturalMCP を使うまでの導入手順

Cursor から Unity Editor の MCP ツール（ログ取得・テスト実行など）を使えるようにする手順です。

---

## 前提

- Unity 6000.0 以上
- Node.js 18 以上
- プロジェクトに **UnityNaturalMCP** と **UniTask** が入っていること

---

## 1. mcp-stdio-to-streamable-http を用意する

Cursor は stdio の MCP しか話せないため、Unity の HTTP サーバーとつなぐ「ブリッジ」が必要です。

1. [mcp-stdio-to-streamable-http](https://github.com/notargs/mcp-stdio-to-streamable-http) をダウンロード or クローンする
2. 中で `npm install` と `npm run build` を実行
3. **重要**: エンドポイントを Unity に合わせる  
   `src/index.ts` の 9 行目を次のようにする（末尾に `/` を付ける）:
   ```ts
   const MCP_SERVER_ENDPOINT = `http://${MCP_SERVER_IP}:${MCP_SERVER_PORT}/mcp/`;
   ```
4. もう一度 `npm run build` して `dist/index.js` を更新

※ すでに上記の形になっているリリースを使っている場合は 3・4 は不要です。

---

## 2. Cursor の MCP 設定（mcp.json）

プロジェクトの `.cursor/mcp.json` に以下を書く（パスは環境に合わせて変更）:

```json
{
  "mcpServers": {
    "unity-natural-mcp": {
      "command": "C:\\Program Files\\nodejs\\node.exe",
      "args": ["C:\\path\\to\\mcp-stdio-to-streamable-http\\dist\\index.js"],
      "env": {
        "MCP_SERVER_IP": "localhost",
        "MCP_SERVER_PORT": "56780"
      }
    }
  }
}
```

- `args`: 上記ブリッジの **dist/index.js** の絶対パス
- `MCP_SERVER_PORT`: Unity の「Unity Natural MCP」設定で指定したポート（デフォルト 56780）

---

## 3. Unity 側の準備

1. Unity Editor でこのプロジェクトを開く
2. **Edit > Project Settings > Unity Natural MCP** を開く
3. ポートが **56780**（または mcp.json の `MCP_SERVER_PORT` と一致）になっているか確認
4. **Refresh** を押して MCP サーバーを起動  
   Console に「Started MCP server at http://localhost:56780/mcp/」が出れば OK

---

## 4. Cursor で接続する

1. **Cursor を一度終了してから起動し直す**（mcp.json を読み直すため）
2. Unity は**先に起動したまま**にしておく
3. Cursor でこのプロジェクトを開き、AI に「Unity のコンソールログを取得して」などと依頼して動作確認

---

## 接続の流れ（イメージ）

```
Cursor
  → mcp-stdio-to-streamable-http（stdio）
    → Unity MCP Server（HTTP localhost:56780/mcp/）
```

- Cursor: stdio で MCP と通信
- ブリッジ: stdio ⇔ HTTP の変換
- Unity: HTTP で MCP サーバーを提供

---

## よくあるつまずき

| 症状 | 確認すること |
|------|----------------|
| 接続できない | ブリッジの URL が **/mcp/**（末尾スラッシュあり）か |
| 接続できない | Unity でプロジェクトを開いたままか、Refresh 済みか |
| 接続できない | mcp.json のポートと Unity の設定が同じか |
| ツールが出ない | Cursor を再起動したか |

---

## 使える主なツール（接続後）

- **GetCurrentConsoleLogs** … Unity のコンソールログを取得
- **GetCompileLogs** … コンパイルエラー・警告を取得
- **ClearConsoleLogs** … コンソールをクリア
- **RefreshAssets** … AssetDatabase.Refresh
- **RunEditModeTests** / **RunPlayModeTests** … テスト実行

以上で「Cursor を使えるようになるまで」の導入手順は完了です。
