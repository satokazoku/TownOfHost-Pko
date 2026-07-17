//TOH_Yを参考にさせて貰いました ありがとうございます
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.Data.Player;
using Assets.InnerNet;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Newtonsoft.Json.Linq;
using TownOfHost;
using UnityEngine.Networking;

[HarmonyPatch]
public class ModNewsHistory
{
    public static List<ModNews> AllModNews = new();
    public static List<ModNews> JsonAndAllModNews = new();
    public static void Init()
    {
        {
            //リンクはこうやるらしい。<nobr><link=\"URL\">Text</nobr></link>
            /*　　テンプレート
            {
                var news = new ModNews
                {
                    Number = 100002,
                    Title = "text",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.23.11.39</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.23.11.39</color>",
                    Text = "text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text"
                    ,
                    Date = "2026-4-20T00:00:00Z"
                };
                AllModNews.Add(news);
            }*/
            {
                var news = new ModNews
                {
                    Number = 100073,
                    Title = "ぬーん",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v4.28.14.60</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v4.28.14.60</color>",
                    Text = "・未完成の役職の削除\n"
                    + "・新役職スモークメーカーの追加\n"
                    ,
                    Date = "2026-5-17"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100072,
                    Title = "そーりー",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v4.28.14.59</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v4.28.14.59</color>",
                    Text = "・ホストがチャット見えなかったバグの修正\n",
                    Date = "2026-5-16"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100070,
                    Title = "波動砲シェリフ？なんだそりゃ　まだ完成してねえよ!!!",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.27.13.51</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.27.13.51</color>",
                    Text = "・New役職　ヒッチハイカーの追加\n"
                    + "・New属性　ジャンボの追加\n"
                    + "・ロケットの追加 From:SuperNewRoles\n"
                    + "・ニムロッドの追加 From:TownOfHOst-Y\n"
                    + "・爆ぜ師の追加 From:SuperNewRoles\n"
                    + "・自殺願望者の追加 From:SuperNewRoles\n"
                    + "・神がタスク終わってなくても勝利できたバグの修正\n"
                    + "・賢者のクールタイムがおかしかったバグの修正\n"
                    + "・波動砲、波動砲ジャッカル、波動砲シェリフのロワーテクストの改行\n"
                    + "・シーアの霊魂が2個以上になると正しく表示されないバグの修正\n"
                    + "・ペットがつかなかったバグの修正\n"
                    + "・忘却者のバグ修正\n"
                    + "・シーアの霊魂数表示を削除\n"
                    + "・弾の軽量化\n"
                    + "・弾のワープをちょい修正\n"
                    + "・タイムリーパーのワープをちょい修正\n"
                    + "・ヒッチハイカーのワープをちょい修正\n"
                    + "・賢者、ニムロッド、爆ぜ師、ヒッチハイカー翻訳\n"
                    + "・ジェスタータスクできないバグの修正\n"
                    + "・ニムロッド修正\n"
                    ,
                    Date = "2026-5-09"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100069,
                    Title = "なんか皆使ってくれてるから4週間後に一気にアプデしようかと思ったけどあげちゃうわ",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.26.13.50</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.26.13.50</color>",
                    Text = "・TOHKのアップデートに対応\n"
                    + "・波動砲ジャッカルのバグ修正\n"
                    + "・波動砲のチャージ中の★が正常に表示されなかったバグの修正\n"
                    + "・波動砲ジャッカルの軽量化\n"
                    + "・チャッターのバグ修正\n"
                    + "・ダブルキラーのバグ修正\n"
                    + "・毒入りパン屋のバグ修正\n"
                    + "・翻訳のバグ修正\n"
                    + "・配信者向けオプションの追加\n"
                    + "・モデレーターの修正\n"
                    + "・ジェスターのバグ修正\n"
                    + "・サンタにナイスゲッサーの追加\n"
                    + "・サンタにオプションの追加\n"
                    + "・pkoコマンドを制御するオプションの追加\n"
                    + "・/cmd hに新しいコマンドを追加\n"
                    + "・id重複の修正\n"
                    + "・New デバフアドオンの追加「スタミナー」\n"
                    + "・プクプクの翻訳\n"
                    + "・プクプクのキルされてから何ターンまで毒復讐を有効かのオプションの追加\n"
                    + "・プクプクにタスク完了時の能力のバリエーションの追加\n"
                    + "・/cmd s rしたルールはアモアスを閉じても残るように変更\n"
                    + "・ピンの画像の変更\n"
                    + "・侍の仕様変更\n"
                    ,
                    Date = "2026-5-04"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100068,
                    Title = "4つめの新役だぜ！",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.25.12.44</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.25.12.44</color>",
                    Text = "・侍の追加 From:SuperNewRoles\n"
                    + "・TOHKのアプデに対応\n"
                    + "・賢者の追加 From:SuperNewRoles (現在未翻訳、あと能力使用後も賢者ステップ健在()\n"
                    + "・新役チャッターの追加\n"
                    ,
                    Date = "2026-4-30"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100067,
                    Title = "波動砲、波動砲ジャッカルを超超超！大幅軽量化しました！！！",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.25.11.43</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.25.11.43</color>",
                    Text = "・あとシーアの霊魂が正常に表示されるようになりました！(みっちーくんあざす)\n"
                    ,
                    Date = "2026-4-29"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100067,
                    Title = "Zzz...",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.25.11.42</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.25.11.42</color>",
                    Text = "・忘却者の修正\n"
                    + "・フレンド招待機能の削除\n"
                    + "・波動砲、波動砲ジャッカルの軽量化\n"
                    ,
                    Date = "2026-4-29"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100066,
                    Title = "だいぶバグが減ってきた実感があるじぇ",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.25.11.41</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.25.11.41</color>",
                    Text = "・パブロフの犬のバグ修正\n"
                    + "・ナイスゲッサーのバグ修正\n"
                    + "・イーターのバグ修正\n"
                    + "モデレーターの追加仕様\n"
                    + "・クレジット追加\n"
                    ,
                    Date = "2026-4-29"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100065,
                    Title = "１日に3回もアプデは流石に草ｗ",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.25.11.40</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.25.11.40</color>",
                    Text = "・サンタの役職説明に不備があったので修正\n"
                    + "・役職ガイドの更新\n"
                    + "・自動スタート時暗転するバグの修正\n"
                    + "ぴけおAIオンライン対応しました(つまり開発者以外もぴけおAI使えるよってこと"
                    + "↑ただし24時間営業ではない　使いたい時しぇとこさんに言っていただければ起動いたします\n"
                    + "・イビルゲッサーに数字が見えないバグの修正\n"
                    ,
                    Date = "2026-4-28"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100064,
                    Title = "ぷれいしていただきありがとうございます:)",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.24.11.40</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.24.11.40</color>",
                    Text = "・モデレーターの修正\n"
                    + "・波動砲の★が正しく表示されないバグの修正(これもみっちーのせいです、許しません^^)\n"
                    + "・波動砲、波動砲ジャッカルのチャージ中とビーム中の★の色を自分の体の色に仕様変更(これで詰めやすくなったね!)"
                    + "↑それに伴い役職説明も変更したぜーい\n"
                    + "・イビルゲッサー、ナイスゲッサーの翻訳\n"
                    + "・新しいボタンの作成　※まだ未完成\n"
                    ,
                    Date = "2026-4-28"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100063,
                    Title = "Pkoってぴけおって読むらしいぜ",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.23.11.39</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.23.11.39</color>",
                    Text = "・チェイサーの追加 From:TownOfHost_Y\n"
                    + "・/cmd hができないバグの修正(ホストも)\n"
                    + "・ダブルキラーのバグ修正...?\n"
                    + "・波動砲、波動砲ジャッカルの軽量化...?(気持ち程度かもしんねー\n"
                    ,
                    Date = "2026-4-28"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100062,
                    Title = "<size=70%>このModに必要なWinnerのシステムを完全に理解したわけでもないのに\r\n「いらないよ」と言ってしまい勝利システムに不具合が発生していました。\r\n申し訳ございませんでした。byみっちー</size>",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v3.23.11.38</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v3.23.11.38</color>",
                    Text = "・波動砲のバグ修正\n"
                    + "・自動スタートの調整\r\n"
                    + "・skむずかしいと言われたので村長、陰陽師、波動砲ジャッカルのskの時間を3秒から1.5秒に短縮、範囲1.0→1.5に変更\n"
                    + "\r\nバニラの試合結果の表示\r\n↑多分すぐ消す\r\n\n"
                    + "・マグロの勝利できなかったバグの修正\n"
                    + "・スペランカーの勝利できなかったバグの修正\n"
                    + "・陰陽師バグ修正\n"
                    + "・神バグ修正\n"
                    + "・イビルゲッサーバグ修正(一部)n"
                    ,
                    Date = "2026-04-27"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100079,
                    Title = "Kのアプデ対応は地味に大変㌨;-;",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v4.27.13.54</color>",
                    ShortTitle = "<color=#FF9631>◆TOH-Pko v4.27.13.54</color>",
                    Text = "TOHKのアップデートに対応したよ！！！！！\n"
                    + "ところで次バージョン何出せばいいの??\n"
                    + "みんな役提案とか待ってるよ！"
                    ,
                    Date = "2026-05-12"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100080,
                    Title = "マッチメイキング実装予定だった。",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v4.27.13.57</color>",
                    ShortTitle = "<color=#FF9631>◆TOH-Pko v4.27.13.57</color>",
                    Text = "<size=80%>"
                    + "マッチメイキング機能実装予定のため公開ルームにすることができるようになっていますが、\n"
                    + "バニラユーザーからは実際に公開ルームとして反映されていないのでバニラのマッチメイキングからは参加することができなくなっています。\n"
                    + "<size=125%>【バグ修正】</size>\n"
                    + "・ジャンボの属性付与infoが表示されなかった問題の修正\n"
                    + "\n<size=125%>【仕様変更】</size>\n"
                    + "・コマンド実行結果の分割量の変更\n"
                    + "\n<size=125%>【設定追加】</size>\n"
                    + " ・陰陽師/式神の秘匿チャットの追加\n"
                    + "\n<size=125%>【追加役職】</size>\n"
                    + "<size=100%>Ⓝバットガール/Batgirl</size>\n"
                    ,
                    Date = "2026-05-14"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100081,
                    Title = "ほくほくのさつまいもはいかが？",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v4.29.14.61</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v4.29.14.61</color>",
                    Text = "・新役職　スタンドマスター、スタンドの追加\n"
                    + "・さつまといもの追加 From:SuperNewRoles\n"
                    + "・シャイボーイを第三陣営に変更\n"
                    + "・イビルゲッサーの会議でidが表示されないバグの修正\n"
                    + "・ヒッチハイカーちょい修正\n"
                    + "・ナイストラッパー仕様修正更\n"
                    + "・自殺願望者の翻訳\n"
                    + "・画像の変更\n"
                    + "・その他の変更\n"
                    ,
                    Date = "2026-5-18"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100082,
                    Title = "波動砲シェリフ開発中止したぜ★",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v4.29.14.62</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v4.29.14.62</color>",
                    Text = "・イビルムービングの追加 From:SuperNewRoles\n"
                    + "・チャッター君バグ修正して復活\n"
                    + "・スタンドマスター色々仕様変更とバグ修正\n"
                    + "・さつまといもの翻訳ミスを修正\n"
                    + "・h r さつまといもで説明がでなかったバグの修正\n"
                    + "・さつまといもが君臨者を視認できないように変更\n"
                    + "・画像の変更\n"
                    + "・その他の変更\n"
                    ,
                    Date = "2026-5-19"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100083,
                    Title = "サイドキックをしやすくしたよ！",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v4.30.14.63</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v4.30.14.63</color>",
                    Text = "・新役職　ナイスリンカーの追加\n"
                    + "・新役職　イビルリンカーの追加\n"
                    + "・新役職　ゾンビの追加\n"
                    + "・ナイステレポーターの追加 From:SuperNewRoles\n"
                    + "・テレポーターの追加 From:SuperNewRoles\n"
                    + "・村長仕様変更\n"
                    + "・サンタ仕様変更\n"
                    + "・陰陽師仕様変更\n"
                    + "・波動砲ジャッカル仕様変更\n"
                    + "・シェリフ仕様変更\n"
                    + "・スイッチシェリフのリストラ\n"
                    + "・神にオプションの追加\n"
                    + "・シャイボーイバグ修正\n"
                    + "・スタンドマスターのバグ修正\n"
                    + "・イビルムービング翻訳\n"
                    + "・波動砲、波動砲ジャッカルを色々調整し軽量化、ビームが見えなかったバグの修正、その他のバグ修正\n"
                    + "・画像変更\n"
                    + "・一定の条件下で会議画面が表示されないバグの修正\n"
                    + "・その他修正\n"
                    ,
                    Date = "2026-5-23"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100084,
                    Title = "私にはまだ早かったみたい(一旦ダミー役職削除)",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v4.30.14.64</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v4.30.14.64</color>",
                    Text = "・波動砲ジャッカルではなく弾が排出されるバグの修正、その他バグ修正\n"
                    + "・ジャンボのバグ修正\n"
                    + "・ムービングがベントできたバグの修正\n"
                    + "・イビルムービングのキルクがおかしかったバグの修正\n"
                    + "・毒パン屋の修正、翻訳、バグ修正\n"
                    + "・村長のバグ修正\n"
                    + "・ナイステレポーターの仕様変更\n"
                    + "・テレポーターの仕様変更\n"
                    + "・ミニマリストの追加 From:SuperNewRoles\n"
                    ,
                    Date = "2026-5-25"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100085,
                    Title = "皆でチャットしようぜ！",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v4.31.14.65</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v4.31.14.65</color>",
                    Text = "・新機能　グローバルチャットのテスト実装\n"
                    + "・バットガールのバグ修正\n"
                    + "・自殺願望者のバグ修正\n"
                    + "・スタンドマスターのバグ修正\n"
                    + "・マッチメイキング修正\n"
                    ,
                    Date = "2026-5-27"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100086,
                    Title = "'-'",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v4.31.14.66</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v4.31.14.66</color>",
                    Text = "・ちょっとした修正\n"
                    ,
                    Date = "2026-5-27"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100087,
                    Title = "悲報:ついっちくんしす",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v4.31.15.67</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v4.31.15.67</color>",
                    Text = "・TOHKのアプデに対応\n"
                    + "・ペット自動付与機能を一旦削除\n"
                    + "・新役職:子方テストリリース\n"
                    ,
                    Date = "2026-5-27"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100088,
                    Title = "ペット君いつ戻ってくるんだ(T_T)",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v4.32.16.68</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v4.32.16.68</color>",
                    Text = "・TOHKのアプデに対応\n"
                    + "・陰陽師の秘匿チャットの翻訳\n"
                    + "・ナイステレポーターのオプションの翻訳\n"
                    + "・キューピッドラバーズの翻訳\n"
                    + "・テレポーターの翻訳のミスの修正\n"
                    + "・いくつかの第三陣営が勝利できなかったバグの修正\n"
                    + "・フリーターの小さいバグ修正\n"
                    + "・その他細かいバグ修正\n"
                    + "・オポチュニストにオプションの追加\n"
                    + "・パブロフ陣営の秘匿チャットの追加\n"
                    + "・フリーターの秘匿チャットの追加\n"
                    + "・スタンドマスターの秘匿チャットの追加\n"
                    + "・キューピッドの秘匿チャットの追加\n"
                    + "・フリーターの役職の表示方法を変更\n"
                    + "・キューピッドの♥の色の変更、役職の表示方法の変更、その他変更\n"
                    + "・ニムロッド仕様変更\n"
                    ,
                    Date = "2026-6-01"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100089,
                    Title = "グローバルチャットでの下ネタ、暴言等やめてください:(",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v4.32.16.69</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v4.32.16.69</color>",
                    Text = "・テレポーターのキルクが2倍になるバグの修正\n・テレポーター、ナイステレポーターのロワーテクストが2重で表示されるバグの修正\n・ナイステレポーター、テレポーターのワープで場外にでちゃうバグの修正\n・モデレーターがコマンド使えなかったバグの修正"
                    ,
                    Date = "2026-6-01"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100090,
                    Title = "結構バグ減ってきたんじゃね？(知らんけど",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v4.32.17.70</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v4.32.17.70</color>",
                    Text = "・TOHKのアプデに対応\n・村長、サンタで任命されたシェリフがたまにキルク0だったバグの修正\r\n・タイムリーパーのキルクのバグ修正\r\n・ナイステレポータークールダウンバグ修正\r\n・フリーターの就職相手が乗っ取り役だと追加勝利できなかったバグの修正\r\n・マッドワーカーがシェリフに任命された際、タスクが完了判定になりインポスターが勝利するバグの修正\r\n・Joinメッセージを変更\r\n"
                    ,
                    Date = "2026-6-03"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100091,
                    Title = "グローバルチャット君またね(",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v4.32.17.71</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v4.32.17.71</color>",
                    Text = "・エボルバーの追加 From:ExtremeRoles\r\n・オポチュニストバグ修正\r\n・忘却者バグ修正\r\n・スタンドマスターバグ修正\r\n・村長の仕様変更\r\n・陰陽師の仕様変更\r\n・サンタの仕様変更\r\n・波動砲ジャッカルの仕様変更\r\n・グローバルチャット封印\r\n"
                    ,
                    Date = "2026-6-07"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100092,
                    Title = "㋒",
                    SubTitle = "<color=#FF9631>Town Of Host-Pko v4.32.17.72</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v4.32.17.72</color>",
                    Text = "・エボルバーの翻訳修正\r\n・シェリフの翻訳\r\n・陰陽師バグ修正\r\n・子方(ゲームマスター)バグ修正\r\n・スクラッチャーの追加\r\n・式神が闇鍋構成でアサインされないように修正\r\n・毒入りパン屋が闇鍋構成でアサインされないように修正\r\n・スタンドが闇鍋構成でアサインされないように修正\r\n・弾が闇鍋構成でアサインされないように修正\r\n・TOWNOFHOST-PKO OTHER2 コマンド設定のオプション追加\r\n・TOWNOFHOST-PKO OTHER2を整理"
                    ,
                    Date = "2026-6-09"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {

                    Number = 100093,
                    Title = "でばっぐばん",
                    SubTitle = "<color=#FF9631>●TOH-Pko v4.32.17.73</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v4.32.17.72</color>",
                    Text = "<size=3.2>【はじめてのデバッグ版配布】</size>\n"
                    + "<size=150%>【新役職】</size>\n"
                    + "・© ワルキューレ\n"
                    + "・© 魔法少女\n"
                    + "<size=150%>【バグ修正】</size>\n"
                    + "イビルムービングでKCDが増加するバグの応急処置<sub>(多分増えません)</sub>\n"
                    + "・ジャッカルウルフとアサシンで併せ持つ役職がダブルキラーの場合\nキルボタンでキルするとファントムクールがリセットされる問題の修正\n"

                    ,
                    Date = "2026-6-11"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100094,
                    Title = "追加されたものが多いね。",
                    SubTitle = "<color=#FF9631>●TOH-Pko v4.32.17.82</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v4.32.17.82</color>",
                    Text = "めんどくさいので雑だけど許してね。覚えてるのだけ書く\n"
                    + "・<size=3>新役職～</size>\n"
                    + "・M マッドシェリフ from:TownOfHost_Y\n"
                    + "・黒猫 from:SuperNewRoles\n"
                    + "C あやしい占い師"
                    + "C ナイス猫又 from:SuperNewRoles\n"
                    + "A 三つ子\n"
                    + "A セキュアラー\n"
                    + "A シーラー\n"
                    + "<size=3>追加された機能</size>\n"
                    + "自動廃村機能(切断人数は落ちたプレイヤーの数を指してます。\n"
                    + "さつまといもにimpが見えるかの設定追加\n"
                    + "シェリフにタスク必要かの設定追加\n"
                    + "多分これだけ..........."
                    ,
                    Date = "2026-6-21"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100095,
                    Title = "中型アップデート..?",
                    SubTitle = "<color=#FF9631>●TOH-Pko v4.32.17.85</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v4.32.17.85</color>",
                    Text = "めんどくさいので雑だけど許してね。覚えてるのだけ書く\n"
                    + "・<size=3>新役職～</size>\n"
                    + "・<#ff1919>Ⓘ ビギナーインポスター</color>\n"
                    + "<size=3>追加された機能</size>\n"
                    + "ジャッカルドールにキラー判定にする設定\n"
                    + "↑の子設定として昇格設定がONのときのみキラー判定にする設定\n"
                    + "コマンド可否設定を許可じゃなくて禁止制に変更\n"
                    + "常時ゲッサーコマンドのやり方表示\n"
                    + "ペットパッチ復活\n"
                    + "投票により自殺願望者とモイラの一時封印\n"
                    + "神勝利時のテキストを神降臨に変更\n"
                    + "自動でロビーに戻る機能で戻るまでの時間を5sに変更\n"
                    + "テレポーターファントムボタンでキルク10sになるバグ修正　\n"
                    + "神が廃村時勝利するバグ修正\n"
                    + "マッチメイキング機能の変更\n"
                    + "ウェブフック機能の変更\n"
                    ,
                    Date = "2026-6-28"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100096,
                    Title = "中途半端だがアップデート",
                    SubTitle = "<color=#FF9631>●TOH-Pko v5.33.18.89</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v5.33.18.89</color>",
                    Text = "・死亡していてもスクラッチできるバグを修正\n"
                    + "・スクラッチャーその他のバグ修正\n"
                    + "・神バグ修正\n"
                    + "・陰陽師バグ修正\n"
                    + "・忘却者オプション翻訳\n"
                    + "・波動砲中に役職変わったら動けなくなるバグを修正\n"
                    + "・賢者能力使用中に役職変わったら動けなくなるバグを修正\n"
                    + "・イビルムービングオプション追加\n"
                    + "・ジャッカルウルフバグ修正\n"
                    + "・アサシンバグ修正\n"
                    + "・シェリフバグ修正\n"
                    + "・サンタのクールタイムのバグ修正とちょっと仕様変更\n"
                    + "・テレポーターロワーテクストのバグ修正\n"
                    + "・超波動砲のビームが表示されないバグの修正\n"
                    + "・牛乳屋翻訳\n"
                    + "・ナイス猫又翻訳\n"
                    + "・黒猫翻訳\n"
                    + "・シェリフオプションの追加\n"
                    + "・ナイスリンカー復活(一時的かもしんねえ)\n"
                    + "・イビルリンカー復活(一時的かもしんねえ)\n"
                    + "・ナイストラッパー復活(一時的かもしんねえ)\n"
                    + "・自殺願望者復活(一時的かもしんねえ)\n"
                    + "・波動砲シェリフ復活(一時的かもしんねえ)\n"
                    + "・新役職ジェイラーの追加\n"
                    + "・新役職ハッピージェスターの追加\n"
                    + "・新役職マスマーダーの追加\n"
                    + "・スウーパーの追加 From:TownOfHost_Enhanced\n"
                    + "・ホストによって使用禁止されているコマンドを/cmd hで表示されないようにしました\n"
                    + "・牛乳屋仕様変更\n"
                    + "・スタンドマスターを強化\n"
                    + "・フリーター仕様変更\n"
                    + "・ペット撫での仕様変更\n"
                    + "・牛乳屋翻訳\n"
                    + "・グローバルチャットの仕様変更(稼働はしません)\n"
                    + "・一旦忘却者リストラ\n"
                    + "・TOH-Kのアプデに対応\n"
                    ,
                    Date = "2026-7-10"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100097,
                    Title = "これから追加よりバグ修正を優先していきまつ",
                    SubTitle = "<color=#FF9631>●TOH-Pko v5.33.18.90</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v5.33.18.90</color>",
                    Text = "・死亡していてもスクラッチできるバグを修正\n"
                    + "・牛乳屋が配達モードで死亡するとタスクできないバグを修正\n"
                    + "・神の役職説明が説明不十分だったので補足\n"
                    + "・神にオプションを追加\n"
                    + "・重複がない秘匿チャットをひとつにまとめた\n"
                    + "・meetinginfoを個別で送信できるように変更、複数選択のオプションでも子オプションを出来るように変更(?)\n"
                    + "・↑これにより牛乳屋仕様変更\n"
                    ,
                    Date = "2026-7-12"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100098,
                    Title = "もう少しで夏休みだじぇ",
                    SubTitle = "<color=#FF9631>●TOH-Pko v5.33.18.91</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Pko v5.33.18.91</color>",
                    Text = "・ラビットバグ修正(?)\n"
                    + "・ナイステレポーターバグ修正\n"
                    + "・テレポーターバグ修正\n"
                    + "・スウーパーバグ修正\n"
                    + "・ジェイラーバグ修正\n"
                    + "・役職の能力で返り討ちをしてしまう鬼のバグを修正\r\n\n"
                    + "・↑同じバグをマッドガーディアンでも修正\n"
                    + "・↑スタンドも\n"
                    + "・フリーター勝利ができないバグを修正\n"
                    + "・パン屋のミーティングインフォが出なかったバグの修正\n"
                    + "・フリーターオプション追加\n"
                    + "・スタンドマスター仕様変更\n"
                    + "・オポチュニストキラーも見えるように陰陽師仕様変更\n"
                    + "・マッチメイキング機能仕様変更\n"
                    + "・警察官リストラ\n"
                    ,
                    Date = "2026-7-13"
                };
                AllModNews.Add(news);
            }
            AnnouncementPopUp.UpdateState = AnnouncementPopUp.AnnounceState.NotStarted;
        }
    }
    //ここもTownOfHost_Y様を参考に..!
    public const string ModNewsURL = "";
    static bool downloaded = false;

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start)), HarmonyPostfix]
    public static void StartPostfix(MainMenuManager __instance)
    {
        static IEnumerator FetchModNews()
        {
            if (downloaded)
            {
                yield break;
            }
            downloaded = true;
            var request = UnityWebRequest.Get(ModNewsURL);
            yield return request.SendWebRequest();
            if (request.isNetworkError || request.isHttpError)
            {
                downloaded = false;
                TownOfHost.Logger.Info("ModNews Error Fetch:" + request.responseCode.ToString(), "ModNews");
                yield break;
            }
            var json = JObject.Parse(request.downloadHandler.text);
            for (var news = json["News"].First; news != null; news = news.Next)
            {
                JsonModNews n = new(
                    int.Parse(news["Number"].ToString()), news["Title"]?.ToString(), news["Subtitle"]?.ToString(), news["Short"]?.ToString(),
                    news["Body"]?.ToString(), news["Date"]?.ToString());
            }
        }
        __instance.StartCoroutine(FetchModNews().WrapToIl2Cpp());
    }

    [HarmonyPatch(typeof(PlayerAnnouncementData), nameof(PlayerAnnouncementData.SetAnnouncements)), HarmonyPrefix]
    public static bool SetModAnnouncements(PlayerAnnouncementData __instance, [HarmonyArgument(0)] ref Il2CppReferenceArray<Announcement> aRange)
    {
        if (AllModNews.Count < 1)
        {
            Init();
            AllModNews.Do(n => JsonAndAllModNews.Add(n));
            JsonAndAllModNews.Sort((a1, a2) => { return DateTime.Compare(DateTime.Parse(a2.Date), DateTime.Parse(a1.Date)); });
        }

        List<Announcement> FinalAllNews = new();
        JsonAndAllModNews.Do(n => FinalAllNews.Add(n.ToAnnouncement()));
        foreach (var news in aRange)
        {
            if (!JsonAndAllModNews.Any(x => x.Number == news.Number))
                FinalAllNews.Add(news);
        }
        FinalAllNews.Sort((a1, a2) => { return DateTime.Compare(DateTime.Parse(a2.Date), DateTime.Parse(a1.Date)); });

        aRange = new(FinalAllNews.Count);
        for (int i = 0; i < FinalAllNews.Count; i++)
            aRange[i] = FinalAllNews[i];

        return true;
    }
}