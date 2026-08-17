# Frontend: Presets Gallery (static)

配置: web フォルダ配下にある静的ファイルを任意の静的ホスティング（Cloudflare Pages など）に置くだけで動作します。

設定:
- API のベースパスは web/index.html の API_BASE 変数を編集してください。デフォルトは '/presets'（同一ドメインで Worker をホストしている場合）。

ダウンロード方法:
1) コピー: クリップボードに設定テキストをコピーします。
2) ファイル保存: Worker の /presets/:id/download?as_file=1 を呼び出してブラウザにファイルを保存します。
3) 直接ロード: ブラウザからローカルの TownOfHost クライアントが待ち受ける http://127.0.0.1:{port}/load-preset に POST します。ポートはプロンプトで入力します。

備考:
- セキュリティのため、直接ロードはローカル接続 (127.0.0.1) のみに限定しています。TownOfHost 側で受信を有効にしておいてください（デフォルトでは options.txt の Preset Data:true と YomiagePort を使用）。
