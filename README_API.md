# Presets API (Cloudflare Workers + D1)

このリポジトリには Cloudflare Workers と D1 を使った "presets" API の実装が含まれます。

## 構成ファイル
- wrangler.toml: Cloudflare Workers 設定（D1 バインディング D1_PRESETS を定義）
- workers/src/index.ts: Worker エントリポイント（HTTP エンドポイント実装）
- src/db/migrations/001_create_presets.sql: D1 用マイグレーション SQL

## D1 にテーブルを作成する
1. Cloudflare ダッシュボードまたは wrangler を使って D1 データベースを作成し、wrangler.toml の `account_id` を設定します。
2. D1 のマイグレーション実行方法は環境により異なります。簡易的にはダッシュボードの SQL エディタに `src/db/migrations/001_create_presets.sql` の内容を貼り付けて実行してください。

## ローカルでの動作確認
1. workers ディレクトリへ移動し、npm install してから `wrangler dev` を実行します。

## エンドポイント
- POST /presets
  - JSON ボディ: { name, creator, config_json, version?, tags? }
  - 戻り値: { id }

- GET /presets
  - クエリ: tags=tag1,tag2 (カンマ区切り), version=1, sort=popular|new, page=1, per_page=20, q=search
  - 戻り値: { total, page, per_page, presets: [...] }

- GET /presets/:id
  - 単一プリセットのメタデータを返す

- POST /presets/:id/download
  - ダウンロード回数をインクリメントして { id, config_json } を返す

## サンプル curl
1) 投稿
curl -X POST https://<your-worker>/_endpoint_/presets \
  -H "Content-Type: application/json" \
  -d '{"name":"テスト","creator":"alice","config_json":"{...}","version":"1","tags":["闇鍋","人外"]}'

2) 一覧
curl "https://<your-worker>/_endpoint_/presets?tags=闇鍋&sort=popular&page=1&per_page=10"

3) ダウンロード
curl -X POST https://<your-worker>/_endpoint_/presets/1/download

## 注意点
- タグはカンマ区切りで保存しているため、スケールした場合はタグ正規化（別テーブル）を推奨します。
- 悪意ある投稿対策（バリデーション、レート制限、モデレーション）は別実装です。
